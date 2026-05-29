using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Scrippy
{
    public abstract class Value : IEquatable<Value>, IComparable<Value> //wrappers for built in types
    {
        public abstract override string ToString();
        public abstract bool isTruthy();
        public abstract bool Equals(Value other);
        public override bool Equals(object obj) { return obj is Value v && this.Equals(v); }
        public abstract override int GetHashCode();
        public virtual bool isHashable() { return false; }
        public abstract string getTypeName();

        protected static readonly Type[] typeOrder = new Type[] //null < bool < num < string < arr < dict
        {
            typeof(NullValue),
            typeof(BoolValue),
            typeof(NumValue),
            typeof(StrValue),
            typeof(ArrValue),
            typeof(DictValue)
        };

        public virtual int CompareTo(Value other)
        {
            int thisIndex = Array.IndexOf(typeOrder, this.GetType());
            int otherIndex = Array.IndexOf(typeOrder, other.GetType());
            if (thisIndex == -1 || otherIndex == -1) { throw new Exception($"Unknown type in comparison: {this.GetType()} or {other.GetType()}"); }
            return thisIndex.CompareTo(otherIndex);
        }
    }

    public class DictValue : Value
    {
        private Dictionary<Value, Value> values;

        public DictValue(Dictionary<Value, Value> values)
        {
            foreach (Value key in values.Keys)
            {
                if (!key.isHashable()) { throw new Exception($"Key {key} is not hashable, cannot be used as a dictionary key"); }
            }
            this.values = values;
        }

        #region Wrapper
        public Value this[Value key]
        {
            get
            {
                if (values.ContainsKey(key)) { return values[key]; }
                throw new Exception($"Key {key} not found in dictionary");
            }
            set
            {
                if (!key.isHashable()) { throw new Exception($"Key {key} is not hashable, cannot be used as a dictionary key"); }
                values[key] = value;
            }
        }
        public bool ContainsKey(Value key)
        {
            if (!key.isHashable()) { throw new Exception($"Key {key} is not hashable, cannot be used as a dictionary key"); }
            return values.ContainsKey(key);
        }
        public int Count { get { return values.Count; } }

        public static DictValue operator +(DictValue d1, DictValue d2) //merge fields
        {
            Dictionary<Value, Value> newValues = new Dictionary<Value, Value>();
            foreach (KeyValuePair<Value, Value> kvp in d1.values)
            {
                newValues[kvp.Key] = kvp.Value;
            }
            foreach (KeyValuePair<Value, Value> kvp in d2.values)
            {
                if (newValues.ContainsKey(kvp.Key)) { throw new Exception($"Duplicate key found when adding dictionaries: {kvp.Key}"); }
                newValues[kvp.Key] = kvp.Value;
            }
            return new DictValue(newValues);
        }

        public static DictValue operator *(DictValue d, NumValue n)
        {
            if (d.values.Count == 0) { return new DictValue(new Dictionary<Value, Value>()); }
            if (!n.isInt() || (long) n != 0) { throw new Exception($"Cannot multiply dictionary by nonzero number: {n}"); }
            return new DictValue(new Dictionary<Value, Value>());
        }
        #endregion

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("[");
            foreach (KeyValuePair<Value, Value> kvp in values)
            {
                sb.Append($"{kvp.Key}: {kvp.Value}, ");
            }
            if (values.Count > 0) { sb.Remove(sb.Length - 2, 2); }//remove last comma and space
            sb.Append("]");
            return sb.ToString() == "[]" ? "[:]" : sb.ToString(); //switch to [:] if empty, distinguish from array
        }

        public override bool isTruthy() { return values.Count > 0; }

        public override bool Equals(Value other)
        {
            if (!(other is DictValue otherDict)) { return false; }
            if (values.Count != otherDict.Count) { return false; }
            bool equal = true;
            foreach (KeyValuePair<Value, Value> kvp in values)
            {
                //if other dict doesnt contain key or value at that key not equal -> break, return false
                if (!otherDict.ContainsKey(kvp.Key) || !kvp.Value.Equals(otherDict[kvp.Key])) { equal = false; break; }
            }
            return equal;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            foreach (KeyValuePair<Value, Value> kvp in values)
            {
                int pairHash = 17;
                pairHash = (pairHash * 31) + (kvp.Key?.GetHashCode() ?? 0);
                pairHash = (pairHash * 31) + (kvp.Value?.GetHashCode() ?? 0);
                hash ^= pairHash;
            }
            return hash;
        }

        public override string getTypeName() { return "dictionary"; }

        public override int CompareTo(Value other)
        {
            return base.CompareTo(other); //dict > all other types + all dicts equal
        }
    }

    public class ArrValue : Value
    {
        private List<Value> values;

        public ArrValue(List<Value> values) { this.values = values; }

        #region Wrapper
        public Value this[int index]
        {
            get
            {
                if (index < values.Count && index >= 0) { return values[index]; }
                else { throw new Exception($"Index {index} out of bounds for array of length {values.Count}"); }
            }
            set
            {
                if (index < values.Count && index >= 0) { values[index] = value; }
                else if (index == values.Count) { values.Add(value); }
                else { throw new Exception($"Index {index} out of bounds for array of length {values.Count}"); }
            }
        }
        public int Count { get { return values.Count; } }

        public static ArrValue operator +(ArrValue a, Value b)
        {
            List<Value> newValues = new List<Value>(a.values);
            if (b is ArrValue bArr) { newValues.AddRange(bArr.values); }
            else { newValues.Add(b); }
            return new ArrValue(newValues);
        }

        public static ArrValue operator *(ArrValue a, NumValue n)
        {
            if (!n.isInt()) { throw new Exception($"Cannot multiply array by non-integer number {n}"); }
            if ((long) n < 0) { throw new Exception($"Cannot multiply array by negative number {n}"); }
            List<Value> values = new List<Value>();
            for (int i = 0; i < (long) n; i++) { values.AddRange(a.values); }
            return new ArrValue(values);
        }

        public static ArrValue operator -(ArrValue a)
        {
            Value[] values = a.values.ToArray();
            Array.Reverse(values);
            return new ArrValue(values.ToList());
        }

        #endregion

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("[");
            foreach (Value v in values)
            {
                sb.Append($"{v}, ");
            }
            if (values.Count > 0) { sb.Remove(sb.Length - 2, 2); } //remove last comma and space
            sb.Append("]");
            return sb.ToString();
        }

        public override bool isTruthy() { return values.Count > 0; }

        public override bool Equals(Value other)
        {
            if (!(other is ArrValue otherArr)) { return false; }
            if (values.Count != otherArr.Count) { return false; }
            bool equal = true;
            for (int i = 0; i < values.Count; i++) { if (!values[i].Equals(otherArr[i])) { equal = false; break; } }
            return equal;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            foreach (Value v in values) { hash = (hash * 31) + (v?.GetHashCode() ?? 0); }
            return hash;
        }

        public override string getTypeName() { return "array"; }

        public override int CompareTo(Value other)
        {
            int otherType = base.CompareTo(other);
            if (otherType != 0) { return otherType; } //arr > null/bool/num/str, arr < dict
            ArrValue a = (ArrValue) other;
            if (a.Count != values.Count) { return values.Count.CompareTo(a.Count); }
            for(int i = 0; i < values.Count; i++)
            {
                int compare = values[i].CompareTo(a[i]);
                if (compare != 0) { return compare; }
            }
            return 0;
        }
    }

    public class BoolValue : Value //singleton -> reduce memory + only 2 values
    {
        public static BoolValue trueInstance { get; } = new BoolValue(true);
        public static BoolValue falseInstance { get; } = new BoolValue(false);

        private bool value;
        private BoolValue(bool value) { this.value = value; }

        #region Wrapper
        public static explicit operator bool(BoolValue b) { return b.value; }
        public static explicit operator BoolValue(bool b) { return b ? trueInstance : falseInstance; }
        public static BoolValue operator !(BoolValue b) { return b.value ? falseInstance : trueInstance; }

        public static BoolValue operator &(BoolValue b1, BoolValue b2) { return (BoolValue) (b1.value && b2.value); }
        public static BoolValue operator |(BoolValue b1, BoolValue b2) { return (BoolValue) (b1.value || b2.value); }
        #endregion

        public override string ToString() { return value ? "true" : "false"; } //not uppercase like c#
        public override bool isTruthy() { return value; }
        public override bool Equals(Value other)
        {
            if (!(other is BoolValue otherBool)) { return false; }
            return value == otherBool.value;
        }

        public override int GetHashCode() { return value.GetHashCode(); }
        public override bool isHashable() { return true; }

        public override string getTypeName() { return "boolean"; }

        public override int CompareTo(Value other)
        {
            return base.CompareTo(other); //bool > null, bool < num/str/arr/dict, all bools equal
        }
    }

    public class NumValue : Value
    {
        private double value;
        public NumValue(double value) { this.value = value; }

        #region Wrapper
        public static explicit operator double(NumValue n) { return n.value; }
        public static explicit operator long(NumValue n) { return (long) n.value; }
        public static explicit operator NumValue(double n) { return new NumValue(n); }
        public static NumValue operator +(NumValue a, NumValue b) { return new NumValue(a.value + b.value); }
        public static NumValue operator -(NumValue a, NumValue b) { return new NumValue(a.value - b.value); }
        public static NumValue operator *(NumValue a, NumValue b) { return new NumValue(a.value * b.value); }
        public static NumValue operator /(NumValue a, NumValue b) { return new NumValue(a.value / b.value); }
        public static NumValue operator %(NumValue a, NumValue b) { return new NumValue(a.value % b.value); }
        public static NumValue operator -(NumValue a) { return new NumValue(-a.value); }
        #endregion

        public override string ToString() { return value.ToString(CultureInfo.InvariantCulture); } //make sure 3.14 dont become 3,14
        public override bool isTruthy() { return value != 0 && !double.IsNaN(value); }
        public override bool Equals(Value other)
        {
            if (!(other is NumValue otherNum)) { return false; }
            return value == otherNum.value; //NaN not equals NaN
        }
        public override int GetHashCode() { return value.GetHashCode(); }
        public override bool isHashable() { return true; }
        public bool isInt() { return value % 1 == 0; } //integers stored accurately until long limit
        public override string getTypeName() { return "number"; }
        public override int CompareTo(Value other)
        {
            int otherType = base.CompareTo(other);
            if (otherType != 0) { return otherType; } //num > null/bool, num < str/arr/dict
            return value.CompareTo(((NumValue) other).value);
        }
    }

    public class StrValue : Value
    {
        private string value;
        public StrValue(string value) { this.value = value; }

        #region Wrapper
        public static explicit operator string(StrValue s) { return s.value; }
        public static StrValue operator +(StrValue a, Value b) { return new StrValue(a.value + b.ToString()); }
        public static StrValue operator *(StrValue s, NumValue n)
        {
            if (!n.isInt()) { throw new Exception($"Cannot multiply string by non-integer number {n}"); }
            if ((long) n < 0) { throw new Exception($"Cannot multiply string by negative number {n}"); }
            StringBuilder sb = new StringBuilder();
            for (long i = 0; i < (long) n; i++) { sb.Append(s.value); }
            return new StrValue(sb.ToString());
        }
        public static StrValue operator -(StrValue s)
        {
            char[] chars = s.value.ToCharArray();
            Array.Reverse(chars);
            return new StrValue(new string(chars));
        }
        public static StrValue operator -(StrValue a, StrValue b)
        {
            if (a.value.EndsWith(b.value)) { return new StrValue(a.value.Substring(0, a.value.Length - b.value.Length)); }
            else { throw new Exception($"{a} does not end with {b}"); }
        }
        #endregion

        public override string ToString() { return value; }
        public override bool isTruthy() { return value.Length > 0; }
        public override bool Equals(Value other)
        {
            if (!(other is StrValue otherString)) { return false; }
            return value == otherString.value;
        }
        public override int GetHashCode() { return value.GetHashCode(); }
        public override bool isHashable() { return true; }
        public override string getTypeName() { return "string"; }
        public override int CompareTo(Value other)
        {
            int otherType = base.CompareTo(other);
            if (otherType != 0) { return otherType; } //str > null/bool/num, str < arr/dict
            StrValue s = (StrValue) other;
            //c# compare letter by letter, then length -> I do length then letter
            if (value.Length != s.value.Length) { return value.Length.CompareTo(s.value.Length); }
            return value.CompareTo(s.value);
        }
    }

    public class NullValue : Value //singleton -> reduce memory + all nulls are the same
    {
        public static NullValue instance { get; } = new NullValue(); //singleton since all nulls are the same
        private NullValue() { } //default is public
        public override string ToString() { return "null"; }
        public override bool isTruthy() { return false; }
        public override bool Equals(Value other) { return other is NullValue; }
        public override int GetHashCode() { return 0; }
        public override bool isHashable() { return true; }
        public override string getTypeName() { return "null"; }
        public override int CompareTo(Value other)
        {
            return base.CompareTo(other); //null < all other types, all nulls equal
        }
    }
}
