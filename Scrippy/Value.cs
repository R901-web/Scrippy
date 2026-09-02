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

        protected static readonly Type[] typeOrder = new Type[] //null < bool < num < string < arr < dict < func < type
        {
            typeof(NullValue),
            typeof(BoolValue),
            typeof(NumValue),
            typeof(StrValue),
            typeof(ArrValue),
            typeof(DictValue),
            typeof(FuncValue),
            typeof(TypeValue),
            typeof(NativeFuncValue)
        };

        public virtual int CompareTo(Value other) //cant compare native funcs
        {
            int thisIndex = Array.IndexOf(typeOrder, this.GetType());
            int otherIndex = Array.IndexOf(typeOrder, other.GetType());
            if (thisIndex == -1) { throw new Exception($"Comparison not supported for type: {this.getTypeName()}"); }
            if (otherIndex == -1) { throw new Exception($"Comparison not supported for type: {other.getTypeName()}"); }
            return thisIndex.CompareTo(otherIndex);
        }

        protected static readonly Dictionary<Type, string> typeNames = new Dictionary<Type, string>
        {
            [typeof(DictValue)] = "dictionary",
            [typeof(ArrValue)] = "array",
            [typeof(StrValue)] = "string",
            [typeof(NumValue)] = "number",
            [typeof(BoolValue)] = "boolean",
            [typeof(NullValue)] = "null",
            [typeof(TypeValue)] = "type",
            [typeof(NativeFuncValue)] = "native function",
            [typeof(FuncValue)] = "function"
        };

        public string getTypeName() { return typeNames[this.GetType()]; }

        public abstract Value clone(); //provide a deep copy

        public virtual Value castTo(TypeValue type)
        {
            if (type.type == typeof(StrValue)) { return new StrValue(ToString()); }
            else if (type.type == typeof(TypeValue)) { return new TypeValue(this); }
            else if (type.type == typeof(BoolValue)) { return (BoolValue) isTruthy(); }
            else if (type.type == this.GetType()) { return clone(); }

            throw new Exception($"Type {getTypeName()} cannot be cast to type {type.ToString()}");
        }
    }

    public class DictValue : Value
    {
        private readonly Dictionary<Value, Value> values;

        public DictValue(Dictionary<Value, Value> values)
        {
            foreach (Value key in values.Keys)
            {
                if (!key.isHashable()) { throw new Exception($"Key {key} is not hashable, cannot be used as a dictionary key"); }
            }
            this.values = new Dictionary<Value, Value>(values);
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

        public int length { get { return values.Count; } }
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
            if (values.Count != otherDict.values.Count) { return false; }
            bool equal = true;
            foreach (KeyValuePair<Value, Value> kvp in values)
            {
                //if other dict doesnt contain key or value at that key not equal -> break, return false
                if (!otherDict.values.ContainsKey(kvp.Key) || !kvp.Value.Equals(otherDict[kvp.Key])) { equal = false; break; }
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

        public override int CompareTo(Value other)
        {
            int otherType = base.CompareTo(other);
            if (otherType != 0) { return otherType; } //arr > null/bool/num/str, arr < dict
            DictValue a = (DictValue) other;
            //sort by keys then compare lexicographically, then length
            List<KeyValuePair<Value, Value>> orderedSelf = values.OrderBy(kvp => kvp.Key).ToList();
            List<KeyValuePair<Value, Value>> orderedOther = a.values.OrderBy(kvp => kvp.Key).ToList();
            for (int i = 0; i < orderedSelf.Count && i < orderedOther.Count; i++)
            {
                int compareKey = orderedSelf[i].Key.CompareTo(orderedOther[i].Key);
                if (compareKey != 0) { return compareKey; }

                int compareValue = orderedSelf[i].Value.CompareTo(orderedOther[i].Value);
                if (compareValue != 0) { return compareValue; }
            }
            return orderedSelf.Count.CompareTo(orderedOther.Count); //if all keys and values equal, shorter dict is less
        }

        public override Value clone()
        {
            Dictionary<Value, Value> newValues = new Dictionary<Value, Value>();
            foreach (KeyValuePair<Value, Value> kvp in values)
            {
                newValues[kvp.Key.clone()] = kvp.Value.clone();
            }
            return new DictValue(newValues);
        }

#warning add casting to objects later
        public override Value castTo(TypeValue type)
        {
            if (type.type == typeof(ArrValue))
            {
                List<Value> newValues = new List<Value>();
                Dictionary<Value, Value> sorted = values.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                foreach (KeyValuePair<Value, Value> kvp in sorted) { newValues.Add(new ArrValue(new List<Value>() { kvp.Key, kvp.Value })); }
                return new ArrValue(newValues);
            }

            return base.castTo(type);
        }

        public bool contains(Value val)
        {
            foreach (KeyValuePair<Value, Value> kvp in values)
            {
                Dictionary<Value, Value> dict = new Dictionary<Value, Value>() { [kvp.Key] = kvp.Value };
                Value v = new DictValue(dict);
                if (v.Equals(val)) { return true; }
            }
            return false;
        }
    }

    public class ArrValue : Value
    {
        private readonly List<Value> values;

        public ArrValue(List<Value> values)
        {
            this.values = new List<Value>(values);
        }

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

        public Value this[Value index]
        {
            get
            {
                if (!(index is NumValue n) || !n.isInt()) { throw new Exception($"Index {index} is not an integer, cannot index array"); }
                return this[(int) n];
            }
            set
            {
                if (!(index is NumValue n) || !n.isInt()) { throw new Exception($"Index {index} is not an integer, cannot index array"); }
                this[(int) n] = value;
            }
        }

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

        //directly modifies the list -> var a = [1, 2, 3]; var b = a; b--; print(a); -> [1, 2] since a and b reference same list
        //previously would print [1, 2, 3] since b-- creates new list
        public static ArrValue operator --(ArrValue a)
        {
            if (a.values.Count == 0) { throw new Exception("Cannot decrement an empty array"); }
            /*
            List<Value> values = new List<Value>();
            foreach (Value v in a.values) { values.Add(v); }
            values.RemoveAt(values.Count - 1); //remove last element
            return new ArrValue(values);
            */
            a.values.RemoveAt(a.values.Count - 1); //remove last element
            return a;
        }

        public int length
        {
            get { return values.Count; }
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
            if (values.Count != otherArr.values.Count) { return false; }
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

        public override int CompareTo(Value other)
        {
            int otherType = base.CompareTo(other);
            if (otherType != 0) { return otherType; } //arr > null/bool/num/str, arr < dict
            ArrValue a = (ArrValue) other;
            for (int i = 0; i < values.Count && i < a.values.Count; i++)
            {
                int compare = values[i].CompareTo(a[i]);
                if (compare != 0) { return compare; }
            }
            return values.Count.CompareTo(a.values.Count); //if all values equal, shorter array is less
        }

        public override Value clone()
        {
            List<Value> newValues = new List<Value>();
            foreach (Value v in values) { newValues.Add(v.clone()); }
            return new ArrValue(newValues);
        }

        public override Value castTo(TypeValue type)
        {
            if (type.type == typeof(DictValue))
            {
                foreach (Value v in values)
                {
                    if (v is ArrValue a && a.length == 2) { continue; }
                    throw new Exception("All values in an array must be in pairs to cast to dictionary");
                }
                Dictionary<Value, Value> dict = new Dictionary<Value, Value>();
                foreach (Value v in values)
                {
                    ArrValue a = (ArrValue) v;
                    if (dict.ContainsKey(a[0])) { throw new Exception("Duplicate key found when casting array to dictionary"); }
                    dict[a[0]] = a[1];
                }
                return new DictValue(dict);
            }
            return base.castTo(type);
        }

        public bool contains(Value val)
        {
            foreach (Value v in values) { if (v.Equals(val)) { return true; } }
            return false;
        }
    }

    public class BoolValue : Value //singleton -> reduce memory + only 2 values
    {
        public static BoolValue trueInstance { get; } = new BoolValue(true);
        public static BoolValue falseInstance { get; } = new BoolValue(false);

        private readonly bool value;
        private BoolValue(bool value) { this.value = value; }

        #region Wrapper
        public static explicit operator bool(BoolValue b) { return b.value; }
        public static explicit operator BoolValue(bool b) { return b ? trueInstance : falseInstance; }
        public static BoolValue operator !(BoolValue b) { return b.value ? falseInstance : trueInstance; }
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

        public override int CompareTo(Value other) //true > false
        {
            int otherType = base.CompareTo(other);
            if (otherType != 0) { return otherType; } //num > null/bool, num < str/arr/dict
            return value.CompareTo(((BoolValue) other).value);
        }

        public override Value clone() { return this; } //immutable, can return self

        public override Value castTo(TypeValue type)
        {
            if (type.type == typeof(NumValue)) { return value ? new NumValue(1) : new NumValue(0); }
            return base.castTo(type);
        }
    }

    public class NumValue : Value
    {
        private readonly double value;

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
        public static NumValue operator ++(NumValue a) { return new NumValue(a.value + 1); }
        public static NumValue operator --(NumValue a) { return new NumValue(a.value - 1); }
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
        public override int CompareTo(Value other)
        {
            int otherType = base.CompareTo(other);
            if (otherType != 0) { return otherType; } //num > null/bool, num < str/arr/dict
            return value.CompareTo(((NumValue) other).value);
        }
        public override Value clone() { return new NumValue(this.value); }
        public override Value castTo(TypeValue type)
        {
            if (type.type == typeof(ArrValue))
            {
                if (!isInt()) { throw new Exception($"Non-integer numbers cannot be cast to arrays"); }
                List<Value> newList = new List<Value>();
                for (int i = 0; i < value; i++) { newList.Add(NullValue.instance); }
                return new ArrValue(newList);
            }
            return base.castTo(type);
        }
    }

    public class StrValue : Value
    {
        private readonly string value;

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
        public static StrValue operator --(StrValue s)
        {
            if (s.value.Length == 0) { throw new Exception("Cannot decrement an empty string"); }
            return new StrValue(s.value.Substring(0, s.value.Length - 1));
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
        public override int CompareTo(Value other)
        {
            int otherType = base.CompareTo(other);
            if (otherType != 0) { return otherType; } //str > null/bool/num, str < arr/dict
            //lexicographic then length
            return value.CompareTo(((StrValue) other).value);
        }
        public override Value clone() { return new StrValue(this.value); }
        public override Value castTo(TypeValue type)
        {
            if (type.type == typeof(NumValue))
            {
                try
                {
                    double numVal;
                    if (value.StartsWith("0x")) { numVal = ulong.Parse(value.Substring(2), NumberStyles.HexNumber); }
                    else if (value.StartsWith("-0x")) { numVal = -1 * (double) ulong.Parse(value.Substring(3), NumberStyles.HexNumber); }
                    else { numVal = double.Parse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign); }
                    return new NumValue(numVal);
                }
                catch (FormatException) { throw new Exception($"{value} cannot be cast to type {type.ToString()}"); }
            }
            if (type.type == typeof(ArrValue))
            {
                List<Value> list = new List<Value>();
                foreach (char c in value) { list.Add(new StrValue(c.ToString())); }
                return new ArrValue(list);
            }

            return base.castTo(type);
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
        public override int CompareTo(Value other) //compring nulls not always false like other langauges
        {
            return base.CompareTo(other); //null < all other types, all nulls equal
        }
        public override Value clone() { return this; } //immutable, can return self
        public override Value castTo(TypeValue type)
        {
            if (type.type == typeof(NumValue)) { return new NumValue(0); }
            else if (type.type == typeof(ArrValue)) { return new ArrValue(new List<Value>()); }
            else if (type.type == typeof(DictValue)) { return new DictValue(new Dictionary<Value, Value>()); }

            return base.castTo(type);
        }
    }

    public class TypeValue : Value
    {
        private readonly Type value;

        public Type type { get { return value; } }

        public TypeValue(Type value)
        {
            if (!value.IsSubclassOf(typeof(Value))) { throw new Exception($"Unsupported type in TypeValue: {value}"); }
            this.value = value;
        }
        public TypeValue(Value v) { this.value = v.GetType(); }

        public override string ToString() { return typeNames[value]; }
        public override bool isTruthy() { return true; }
        public override bool Equals(Value other) { return other is TypeValue t && t.value == value; }
        public override int GetHashCode() { return value.GetHashCode(); }
        public override bool isHashable() { return true; }
        public override int CompareTo(Value other)
        {
            int otherType = base.CompareTo(other); //compare normally to toher types
            if (otherType != 0) { return otherType; }

            TypeValue t = (TypeValue) other; //else compare the types stored
            int thisIndex = Array.IndexOf(typeOrder, this.value);
            int otherIndex = Array.IndexOf(typeOrder, t.value);
            if (thisIndex == -1 || otherIndex == -1) { throw new Exception($"Unsupported type in TypeValue: {this.value} or {t.value}"); }
            return thisIndex.CompareTo(otherIndex);
        }
        public override Value clone() { return this; }
    }

    public class FuncValue : Value
    {
        private readonly Token[] param;
        private readonly Stmt[] body;
        private readonly Environment scope;
        private readonly Dictionary<string, Value> boundArgs;

        public FuncValue(Token[] param, Stmt[] body, Environment scope) : this(param, body, scope, new Dictionary<string, Value>()) { }

        public FuncValue(Token[] param, Stmt[] body, Environment scope, Dictionary<string, Value> boundArgs)
        {
            this.param = param;
            this.body = body;
            this.scope = scope;
            this.boundArgs = boundArgs;
        }

        #region Wrapper
        public Token token(int index) { return param[index]; }
        public Stmt statement(int index) { return body[index]; }
        public int arity { get { return param.Length; } }
        public int length { get { return body.Length; } }
        public Environment closure { get { return scope; } }
        public Dictionary<string, Value> curriedArgs
        {
            get
            {
                Dictionary<string, Value> temp = new Dictionary<string, Value>();
                foreach (KeyValuePair<string, Value> kvp in boundArgs) { temp.Add(kvp.Key, kvp.Value); }
                return temp;
            }
        }
        #endregion

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("func(");
            for (int i = 0; i < param.Length; i++)
            {
                sb.Append(param[i].source);
                if (i < param.Length - 1) { sb.Append(','); }
            }
            sb.Append(")");
            return sb.ToString();
        }

        public override int GetHashCode()
        {
            FuncValue f = normalize();
            int hash = 17;
            foreach (Token t in f.param) { hash = (hash * 31) + t.source.GetHashCode(); }
            foreach (Stmt s in f.body) { hash = (hash * 31) + s.GetHashCode(); }
            foreach (KeyValuePair<string, Value> kvp in f.boundArgs)
            {
                int pairHash = 17;
                pairHash = (pairHash * 31) + kvp.Key.GetHashCode();
                pairHash = (pairHash * 31) + kvp.Value.GetHashCode();
                hash ^= pairHash;
            }
            return hash;
        }

        public override bool Equals(Value other)
        {
            if (!(other is FuncValue f)) { return false; }
            if (ReferenceEquals(this, other)) { return true; }
            if (this.param.Length != f.param.Length) { return false; }
            if (this.body.Length != f.body.Length) { return false; }
            if (this.boundArgs.Count != f.boundArgs.Count) { return false; }

            FuncValue normalThis = normalize();
            FuncValue normalOther = f.normalize();
            for (int i = 0; i < normalThis.param.Length; i++) { if (normalThis.param[i].source != normalOther.param[i].source) { return false; } }
            foreach (KeyValuePair<string, Value> kvp in normalThis.boundArgs)
            {
                if (!normalOther.boundArgs.TryGetValue(kvp.Key, out Value v)) { return false; }
                if (!v.Equals(kvp.Value)) { return false; }
            }
            for (int i = 0; i < normalThis.body.Length; i++) { if (!normalThis.body[i].Equals(normalOther.body[i])) { return false; } }
            return true;
        }

        public override Value clone()
        {
            Stmt[] newBody = new Stmt[body.Length];
            Token[] newParam = new Token[param.Length];
            Dictionary<string, Value> newBoundArgs = new Dictionary<string, Value>();
            for (int i = 0; i < body.Length; i++) { newBody[i] = body[i]; }
            for (int i = 0; i < param.Length; i++) { newParam[i] = param[i]; }
            foreach (KeyValuePair<string, Value> kvp in boundArgs) { newBoundArgs[kvp.Key] = kvp.Value.clone(); }
            return new FuncValue(newParam, newBody, scope, newBoundArgs);
        }
        public override bool isTruthy() { return body.Length > 0; }
        public override bool isHashable() { return true; }
        public override int CompareTo(Value other) { return base.CompareTo(other); }

        private FuncValue normalize()
        {
            int nameNum = 0;
            Stack<Dictionary<string, string>> newNames = new Stack<Dictionary<string, string>>(); //from old names to new names
            newNames.Push(new Dictionary<string, string>());

            List<Token> newParams = new List<Token>();
            foreach (Token t in param)
            {
                nameNum++;
                newNames.Peek()[t.source] = $"param^{nameNum}";
                newParams.Add(new Token(t.type, lookUp(t.source), t.literal, t.lineStart));
            }
            foreach (KeyValuePair<string, Value> kvp in boundArgs)
            {
                nameNum++;
                newNames.Peek()[kvp.Key] = $"boundArg^{nameNum}";
            }
            string lookUp(string name)
            {
                foreach (Dictionary<string, string> scope in newNames) { if (scope.TryGetValue(name, out string result)) { return result; } }
                return name;
            }
            Expr normalizeExpr(Expr e) //rename only variables in param OR declared in func
            {
                if (e == null) { return null; }
                switch (e)
                {
                    case BinaryExpr b: return new BinaryExpr(normalizeExpr(b.left), b.op, normalizeExpr(b.right));
                    case GroupingExpr g: return new GroupingExpr(normalizeExpr(g.expr), g.lineStart, g.lineEnd);
                    case LiteralExpr l: return new LiteralExpr(l.value, l.lineStart, l.lineEnd);
                    case UnaryExpr u: return new UnaryExpr(u.op, normalizeExpr(u.right));
                    case TernaryExpr t: return new TernaryExpr(normalizeExpr(t.left), t.mainOp, normalizeExpr(t.mid), t.sideOp, normalizeExpr(t.right));
                    case ArrayExpr a:
                        List<Expr> aExprs = new List<Expr>();
                        foreach (Expr e2 in a.elements) { aExprs.Add(normalizeExpr(e2)); }
                        return new ArrayExpr(aExprs, a.lineStart, a.lineEnd);
                    case DictExpr d:
                        Dictionary<Expr, Expr> dExprs = new Dictionary<Expr, Expr>();
                        foreach (KeyValuePair<Expr, Expr> kvp in d.elements) { dExprs.Add(normalizeExpr(kvp.Key), normalizeExpr(kvp.Value)); }
                        return new DictExpr(dExprs, d.lineStart, d.lineEnd);
                    case VarExpr v: return new VarExpr(new Token(v.name.type, lookUp(v.name.source), v.name.literal, v.name.lineStart));
                    case AssignExpr a: return new AssignExpr(normalizeExpr(a.name), normalizeExpr(a.newValue));
                    case IncrExpr i: return new IncrExpr(normalizeExpr(i.name), i.incrType, i.isPost);
                    case BlockExpr b2:
                        newNames.Push(new Dictionary<string, string>());
                        List<Stmt> b2Stmts = new List<Stmt>();
                        foreach (Stmt s in b2.statements) { b2Stmts.Add(normalizeStmt(s)); }
                        Expr expr = normalizeExpr(b2.last);
                        newNames.Pop();
                        return new BlockExpr(b2Stmts.ToArray(), expr);
                    case CallExpr c:
                        List<Expr> cExprs = new List<Expr>();
                        foreach (Expr e2 in c.arguments) { cExprs.Add(normalizeExpr(e2)); }
                        return new CallExpr(normalizeExpr(c.caller), cExprs.ToArray(), c.lineEnd);
                    case FuncExpr f:
                        newNames.Push(new Dictionary<string, string>());
                        List<Token> fNames = new List<Token>();
                        foreach (Token t in f.param)
                        {
                            nameNum++;
                            newNames.Peek()[t.source] = $"funcParam^{nameNum}";
                            fNames.Add(new Token(t.type, lookUp(t.source), t.literal, t.lineStart));
                        }
                        List<Stmt> fStmts = new List<Stmt>();
                        foreach (Stmt s3 in f.body) { fStmts.Add(normalizeStmt(s3)); }
                        newNames.Pop();
                        return new FuncExpr(fNames.ToArray(), fStmts.ToArray(), f.lineStart, f.lineEnd);
                }
                return null;
            }
            Stmt normalizeStmt(Stmt s)
            {
                if (s == null) { return null; }
                switch (s)
                {
                    case ExprStmt e: return new ExprStmt(normalizeExpr(e.expr), e.lineEnd);
                    case VarDeclStmt v:
                        List<Token> vNames = new List<Token>();
                        Expr init = normalizeExpr(v.initializer);
                        foreach (Token t in v.names)
                        {
                            nameNum++;
                            newNames.Peek()[t.source] = $"varDecl^{nameNum}";
                            vNames.Add(new Token(t.type, lookUp(t.source), t.literal, t.lineStart));
                        }
                        return new VarDeclStmt(vNames.ToArray(), init, v.isConst, v.lineStart, v.lineEnd);
                    case ArrDestrStmt a:
                        List<Token> aNames = new List<Token>();
                        Expr init2 = normalizeExpr(a.initializer);
                        foreach (Token t in a.names)
                        {
                            nameNum++;
                            newNames.Peek()[t.source] = $"arrDestr^{nameNum}";
                            aNames.Add(new Token(t.type, lookUp(t.source), t.literal, t.lineStart));
                        }
                        return new ArrDestrStmt(aNames.ToArray(), init2, a.isConst, a.lineStart, a.lineEnd);
                    case DictDestrStmt d:
                        Dictionary<Token, Expr> dNames = new Dictionary<Token, Expr>();
                        Expr init3 = normalizeExpr(d.initializer);
                        foreach (KeyValuePair<Token, Expr> kvp in d.names)
                        {
                            nameNum++;
                            newNames.Peek()[kvp.Key.source] = $"dictDestr^{nameNum}";
                            dNames.Add(new Token(kvp.Key.type, lookUp(kvp.Key.source), kvp.Key.literal, kvp.Key.lineStart), normalizeExpr(kvp.Value));
                        }
                        return new DictDestrStmt(dNames, init3, d.isConst, d.lineStart, d.lineEnd);
                    case BlockStmt b:
                        newNames.Push(new Dictionary<string, string>());
                        List<Stmt> bStmts = new List<Stmt>();
                        foreach (Stmt s2 in b.statements) { bStmts.Add(normalizeStmt(s2)); }
                        newNames.Pop();
                        return new BlockStmt(bStmts.ToArray(), b.lineStart, b.lineEnd);
                    case IfStmt i: return new IfStmt(normalizeExpr(i.condition), normalizeStmt(i.ifBranch), normalizeStmt(i.elseBranch), i.lineStart, i.lineEnd);
                    case WhileStmt w2: return new WhileStmt(normalizeExpr(w2.condition), normalizeStmt(w2.body), normalizeStmt(w2.change), w2.lineStart, w2.lineEnd);
                    case FuncDeclStmt f:
                        nameNum++;
                        newNames.Peek()[f.name.source] = $"funcDecl^{nameNum}";
                        newNames.Push(new Dictionary<string, string>());
                        List<Token> fNames = new List<Token>();
                        foreach (Token t in f.param)
                        {
                            nameNum++;
                            newNames.Peek()[t.source] = $"funcDeclParam^{nameNum}";
                            fNames.Add(new Token(t.type, lookUp(t.source), t.literal, t.lineStart));
                        }
                        List<Stmt> fStmts = new List<Stmt>();
                        foreach (Stmt s3 in f.body) { fStmts.Add(normalizeStmt(s3)); }
                        newNames.Pop();
                        return new FuncDeclStmt(new Token(f.name.type, lookUp(f.name.source), f.name.literal, f.name.lineStart), fNames.ToArray(), fStmts.ToArray(), f.lineStart, f.lineEnd);
                    case JumpStmt j: return new JumpStmt(j.keyword, normalizeExpr(j.value), j.lineStart, j.lineEnd);
                }
                return null;
            }

            List<Stmt> newBody = new List<Stmt>();
            foreach (Stmt s in body) { newBody.Add(normalizeStmt(s)); }

            Dictionary<string, Value> newBoundArgs = new Dictionary<string, Value>();
            foreach (KeyValuePair<string, Value> kvp in boundArgs) { newBoundArgs.Add(lookUp(kvp.Key), kvp.Value); }

            return new FuncValue(newParams.ToArray(), newBody.ToArray(), closure, newBoundArgs);
        }
    }

    public class NativeFuncValue : Value
    {
        private readonly string name;
        private readonly Func<Value[], Value> func;

        #region Wrapper
        public Value call(Value[] args) { return func(args); }

        #endregion

        public NativeFuncValue(Func<Value[], Value> func, string name) { this.func = func; this.name = name; }
        public override string ToString() { return name; }
        public override bool isTruthy() { return true; }
        public override bool Equals(Value other) { return other is NativeFuncValue n && n.name == this.name; }
        public override int GetHashCode()
        {
            int hash = 17;
            hash = (hash * 31) + func.GetHashCode();
            hash = (hash * 31) + name.GetHashCode();
            return hash;
        }
        public override bool isHashable() { return true; }
        public override int CompareTo(Value other) { return base.CompareTo(other); }
        public override Value clone() { return this; } //immutable, can return self
    }
}
