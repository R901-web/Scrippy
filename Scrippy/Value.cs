using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace Scrippy
{
    public abstract class Value //wrappers for dictionaries/lists
    {
        public abstract string stringify();
        public abstract bool isTruthy();
        public abstract bool deepEquals(Value other);

        public override bool Equals(object obj)
        {
            return obj is Value v && this.deepEquals(v);
        }
        public abstract override int GetHashCode();
        public virtual bool isHashable() { return false; }

        public static Value fromLiteral(object literal)
        {
            if (literal is Value v) { return v; }

            if (literal == null) { return NullValue.instance; }
            else if (literal is bool b) { return b ? BoolValue.trueValue : BoolValue.falseValue; }
            else if (literal is double d) { return new NumValue(d); }
            else if (literal is string s) { return new StringValue(s); }
            else if (literal is List<object> list) 
            { 
                List<Value> values = new List<Value>();
                foreach (object item in list) { values.Add(fromLiteral(item)); }
                return new ArrValue(values);
            }
            else if (literal is Dictionary<object, object> dict)
            {
                Dictionary<Value, Value> values = new Dictionary<Value, Value>();
                foreach (KeyValuePair<object, object> kvp in dict) { values.Add(fromLiteral(kvp.Key), fromLiteral(kvp.Value)); }
                return new DictValue(values);
            }
            else { throw new Exception($"Unsupported literal type: {literal.GetType()}"); }
        }
    }

    public class DictValue : Value
    {
        private Dictionary<Value, Value> values;

        public DictValue() { values = new Dictionary<Value, Value>(); }
        public DictValue(Dictionary<Value, Value> values) 
        { 
            foreach (Value key in values.Keys)
            {
                if (!key.isHashable()) { throw new Exception($"Key {key.stringify()} is not hashable, cannot be used as a dictionary key"); }
            }
            this.values = values; 
        }

        #region Wrapper
        public Value this[Value key]
        {
            get
            {
                if (values.ContainsKey(key)) { return values[key]; }
#warning change to custom exception?
                else { throw new Exception($"Key {key.stringify()} not found in dictionary"); }
            }
            set 
            { 
                if (!key.isHashable()) { throw new Exception($"Key {key.stringify()} is not hashable, cannot be used as a dictionary key"); }
                values[key] = value; 
            }
        }
        public bool ContainsKey(Value key) 
        { 
            if (!key.isHashable()) { throw new Exception($"Key {key.stringify()} is not hashable, cannot be used as a dictionary key"); }
            return values.ContainsKey(key); 
        }
        public int Count { get { return values.Count; } }
        #endregion

        public override string stringify()
        {
            StringBuilder sb = new StringBuilder("[");
            foreach (KeyValuePair<Value, Value> kvp in values)
            {
                sb.Append($"{kvp.Key.stringify()}: {kvp.Value.stringify()}, ");
            }
            if (values.Count > 0) { sb.Remove(sb.Length - 2, 2); }//remove last comma and space
            sb.Append("]");
            return sb.ToString() == "[]" ? "[:]" : sb.ToString(); //switch to [:] if empty, distinguish from array
        }

        public override bool isTruthy() { return values.Count > 0; }

        public override bool deepEquals(Value other)
        {
            if (!(other is DictValue otherDict)) { return false; }
            if (values.Count != otherDict.Count) { return false; }
            bool equal = true;
            foreach (KeyValuePair<Value, Value> kvp in values)
            {
                //if other dict doesnt contain key or value at that key not equal -> break, return false
                if (!otherDict.ContainsKey(kvp.Key) || !kvp.Value.deepEquals(otherDict[kvp.Key])){ equal = false; break; }
            }
            return equal;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            foreach (KeyValuePair<Value, Value> kvp in values)
            {
                int pairHash = 17;
                pairHash = pairHash * 31 + (kvp.Key?.GetHashCode() ?? 0);
                pairHash = pairHash * 31 + (kvp.Value?.GetHashCode() ?? 0);
                hash ^= pairHash;
            }
            return hash;
        }
    }

    public class ArrValue : Value
    {
        private List<Value> values;

        public ArrValue() { values = new List<Value>(); }
        public ArrValue(List<Value> values) { this.values = values; }

        #region Wrapper
        public Value this[int index]
        {
            get
            {
                if (index < values.Count && index >= 0) { return values[index]; }
#warning change to custom exception?
                else { throw new Exception($"Index {index} out of bounds for array of length {values.Count}"); }
            }
            set
            {
                if (index < values.Count && index >= 0) { values[index] = value; }
                if (index == values.Count) { values.Add(value); }
                else { throw new Exception($"Index {index} out of bounds for array of length {values.Count}"); }
            }
        }
        public int Count { get { return values.Count; } }
        #endregion

        public override string stringify()
        {
            StringBuilder sb = new StringBuilder("[");
            foreach(Value v in values)
            {
                sb.Append($"{v.stringify()}, ");
            }
            if (values.Count > 0) { sb.Remove(sb.Length - 2, 2); } //remove last comma and space
            sb.Append("]");
            return sb.ToString();
        }

        public override bool isTruthy() { return values.Count > 0; }

        public override bool deepEquals(Value other)
        {
            if (!(other is ArrValue otherArr)) { return false; }
            if (values.Count != otherArr.Count) { return false; }
            bool equal = true;
            for (int i = 0; i < values.Count; i++) { if (!values[i].deepEquals(otherArr[i])) { equal = false; break; } }
            return equal;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            foreach(Value v in values) { hash = hash * 31 + (v?.GetHashCode() ?? 0); }
            return hash;
        }
    }

    public class BoolValue : Value //singleton -> reduce memory + only 2 values
    {
        public static BoolValue trueValue { get; } = new BoolValue(true);
        public static BoolValue falseValue { get; } = new BoolValue(false);

        private bool value;
        private BoolValue(bool value) { this.value = value; }
        public override string stringify() { return value ? "true" : "false"; } //not uppercase like c#
        public override bool isTruthy() { return value; }
        public override bool deepEquals(Value other)
        {
            if (!(other is BoolValue otherBool)) { return false; }
            return value == otherBool.value;
        }

        public override int GetHashCode() { return value.GetHashCode(); }
        public override bool isHashable() { return true; }
    }

    public class NumValue : Value
    {
        private double value;
        public NumValue(double value) { this.value = value; }
        public override string stringify() { return value.ToString(CultureInfo.InvariantCulture); } //make sure 3.14 dont become 3,14
        public override bool isTruthy() { return value != 0 && !double.IsNaN(value); }
        public override bool deepEquals(Value other)
        {
            if (!(other is NumValue otherNum)) { return false; }
            return value == otherNum.value; //NaN not equals NaN
        }

        public override int GetHashCode() { return value.GetHashCode(); }
        public override bool isHashable() { return true; }
    }

    public class StringValue : Value
    {
        private string value;
        public StringValue(string value) { this.value = value; }
        public override string stringify() { return value; }
        public override bool isTruthy() { return value.Length > 0; }
        public override bool deepEquals(Value other)
        {
            if (!(other is StringValue otherString)) { return false; }
            return value == otherString.value;
        }
        public override int GetHashCode() { return value.GetHashCode(); }
        public override bool isHashable() { return true; }

    }

    public class NullValue : Value //singleton -> reduce memory + all nulls are the same
    {
        public static NullValue instance { get; } = new NullValue(); //singleton since all nulls are the same
        private NullValue() { } //default is public
        public override string stringify() { return "null"; }
        public override bool isTruthy() { return false; }
        public override bool deepEquals(Value other) { return other is NullValue; }
        public override int GetHashCode() { return 0; }
        public override bool isHashable() { return true; }
    }
}
