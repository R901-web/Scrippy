using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Policy;

namespace Scrippy
{
    internal class Interpreter
    {
        public IExpr ast { get; }

        public Interpreter(IExpr ast)
        {
            this.ast = ast;
        }

        public object interpretAST()
        {
            try { return evaluate(ast); }
            catch(Diagnostic d) when (d.severity == DiagnosticLevel.ERROR)
            {
                DiagnosticHandler.add(d);
                return null;
            }
        }

        #region Evaluate
        private object evaluate(IExpr node)
        {
            switch (node)
            {
                case BinaryExpr binary:
                    return evaluateBinary(binary);
                case GroupingExpr grouping:
                    return evaluateGrouping(grouping);
                case LiteralExpr literal:
                    return evaluateLiteral(literal);
                case UnaryExpr unary:
                    return evaluateUnary(unary);
                case TernaryExpr ternary:
                    return evaluateTernary(ternary);
                case ArrayExpr array:
                    return evaluateArray(array);
                case DictExpr dict:
                    return evaluateDict(dict);
            }

            return null;
        }

        private object evaluateBinary(BinaryExpr node)
        {
            object left = evaluate(node.left);
            object right = evaluate(node.right); //be eager not lazy

            Type leftT = left?.GetType();
            Type rightT = right?.GetType();

            switch (node.op.type)
            {
                //Arithmetic
                case TokenType.Plus:
                case TokenType.Minus:
                case TokenType.Div:
                case TokenType.Mult:
                case TokenType.Mod:
                case TokenType.Power:
                    return evaluateArithmetic(left, right, node.op);
                //Comparison
                case TokenType.Less:
                case TokenType.LessEQ:
                case TokenType.More:
                case TokenType.MoreEQ:
                case TokenType.Spaceship:
                    return evaluateComparison(left, right, node.op);
                //Equality
                case TokenType.RefEQ:
                case TokenType.Equal:
                case TokenType.NotEQ:
                    return evaluateEquality(left, right, node.op);
                //Logical
                case TokenType.And:
                case TokenType.Or:
                    return evaluateLogical(left, right, node.op);
                //Fallback
                case TokenType.Elvis:
                case TokenType.NullCoalesce:
                    return evaluateFallback(left, right, node.op);
            }
            return null;
        }

        #region Binary Helpers

        private object evaluateArithmetic(object left, object right, Token op)
        {
            switch(op.type)
            {
                case TokenType.Plus:
                    if (left is double && right is double) { return (double) left + (double) right; }
                    if (left is string) { return (string) left + stringify(right); }
                    if (left is List<object> ll && right is List<object> lr) { return ll.Concat(lr).ToList(); }
                    if (left is List<object> ll1) { return ll1.Append(right).ToList(); }
                    if (left is Dictionary<object, object> dl && right is Dictionary<object, object> dr)
                    {
                        try { return dl.Concat(dr).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); }
                        catch (ArgumentException) { throw error(op, $"Duplicate key found when adding two dictionaries together: '{stringify(dl.Keys.Intersect(dr.Keys).First())}'"); }
                    }
                    break;

                case TokenType.Minus:
                    if (left is double && right is double) { return (double) left - (double) right; }
                    if (left is string sl && right is string sr) 
                    { 
                        if (sl.EndsWith(sr)) { return sl.Substring(0, sl.Length - sr.Length); }
                        throw error(op, $"String {sl} does not end with {sr}");
                    }
                    break;

                case TokenType.Mult:
                    if (left is double && right is double) { return (double) left * (double) right; }
                    //if right = 0 -> empty string/list -> dict cant multiply because duplicate keys
                    if (left is string && isNonNegativeInt(right)) { return string.Concat(Enumerable.Repeat(left, (int) (double) right)); }
                    if (left is List<object> ll2 && isNonNegativeInt(right)) { return Enumerable.Repeat(ll2, (int) (double) right).SelectMany(x => x).ToList(); }
                    if (left is Dictionary<object, object> && isNonNegativeInt(right) && (int) (double) right == 0) { return new Dictionary<object, object>(); } //empty dictionary
                    break;

                case TokenType.Div:
                    return (double) left / (double) right;
                case TokenType.Mod:
                    return (double) left % (double) right; //yay c# modulus is truncation
                case TokenType.Power:
                    return Math.Pow((double) left, (double) right);
            }

            return null; //should be unreachable
        }
        private object evaluateComparison(object left, object right, Token op)
        {
            switch (op.type)
            {
#warning add lists + dictionaries
                case TokenType.Less:
                    if (left == null && right == null) { return false; }
                    if (left is double && right is double) { return (double) left < (double) right; }
                    if (left is string && right is string) { return string.CompareOrdinal((string) left, (string) right) < 0; }
                    break;
                case TokenType.More:
                    if (left == null && right == null) { return false; }
                    if (left is double && right is double) { return (double) left > (double) right; }
                    if (left is string && right is string) { return string.CompareOrdinal((string) left, (string) right) > 0; }
                    break;
                case TokenType.LessEQ:
                    if (left == null && right == null) { return false; }
                    if (left is double && right is double) { return (double) left <= (double) right; }
                    if (left is string && right is string) { return string.CompareOrdinal((string) left, (string) right) <= 0; }
                    break;
                case TokenType.MoreEQ:
                    if (left == null && right == null) { return false; }
                    if (left is double && right is double) { return (double) left >= (double) right; }
                    if (left is string && right is string) { return string.CompareOrdinal((string) left, (string) right) >= 0; }
                    break;
                case TokenType.Spaceship:
                    if (left == null && right == null) { return 0; }
                    if (left is double && right is double) { return ((double) left).CompareTo((double) right); }
                    if (left is string && right is string) { return string.CompareOrdinal((string) left, (string) right); }
                    break;
            }
            return null; //should be unreachable
        }

        private object evaluateEquality(object left, object right, Token op)
        {
            switch (op.type)
            {
                case TokenType.RefEQ: //for primitives is always true, others is reference equality
                    if (left == null && right == null) { return true; }
                    if (left is double && right is double) { return (double) left == (double) right; }
                    if (left is bool && right is bool) { return (bool) left == (bool) right; }
                    return Object.ReferenceEquals(left, right);
                case TokenType.Equal:
                    if (left is double && right is double) { return (double) left == (double) right; }
                    if (left is bool && right is bool) { return (bool) left == (bool) right; }
                    if (left is string && right is string) { return (string) left == (string) right; }
                    if (left is List<object> ll3 && right is List<object> lr3) { return deepEqual(ll3, lr3); }
                    if (left is Dictionary<object, object> dl3 && right is Dictionary<object, object> dr3) { return deepEqual(dl3, dr3); }
                    if (left == null && right == null) { return true; }
                    return false;
                case TokenType.NotEQ:
                    if (left is double && right is double) { return (double) left != (double) right; }
                    if (left is bool && right is bool) { return (bool) left != (bool) right; }
                    if (left is string && right is string) { return (string) left != (string) right; }
                    if (left is List<object> ll4 && right is List<object> lr4) { return !deepEqual(ll4, lr4); }
                    if (left is Dictionary<object, object> dl4 && right is Dictionary<object, object> dr4) { return !deepEqual(dl4, dr4); }
                    if (left == null && right == null) { return false; }
                    return true;
            }
            return null; //should be unreachable
        }

        private object evaluateLogical(object left, object right, Token op)
        {
            switch (op.type)
            {
                case TokenType.And:
                    return (bool?) left & (bool?) right; //yay 3-value logic
                case TokenType.Or:
                    return (bool?) left | (bool?) right;
            }
            return null; //should be unreachable
        }

        private object evaluateFallback(object left, object right, Token op)
        {
            switch(op.type)
            {
                case TokenType.NullCoalesce:
                    return left ?? right;
                case TokenType.Elvis:
                    return isTruthy(left) ? left : right; //implicit conversions for elvis, not for ternary -> ternary is explicit
            }
            return null; //should be unreachable
        }
        #endregion

        private object evaluateGrouping(GroupingExpr node) { return evaluate(node.expr); }

        private object evaluateLiteral(LiteralExpr node) { return node.value; } //LiteralExpr will not contain more IExpr

        private object evaluateUnary(UnaryExpr node)
        {
            object right = evaluate(node.right);
            switch (node.op.type)
            {
                case TokenType.Not:
                    return !(bool?) right;
                case TokenType.Minus:
                    if (right is string s) { char[] c = s.ToCharArray(); Array.Reverse(c); return new string(c); }
                    if (right is double) { return -(double) right; }
                    break;
                case TokenType.Plus:
                    return +(double) right;
            }
            return null; //should be unreachable
        }

        private object evaluateTernary(TernaryExpr node)
        {
            object left = evaluate(node.left);
            object mid = evaluate(node.mid);
            object right = evaluate(node.right);
            if (node.mainOp.type == TokenType.TernCond && node.sideOp.type == TokenType.Colon)
            {
                return (bool) left ? mid : right;
            }
#warning do range

            return null;
        }

        private object evaluateArray(ArrayExpr node)
        {
            List<object> elements = new List<object>();
            foreach (IExpr element in node.elements)
            {
                elements.Add(evaluate(element));
            }
            return elements;
        }

        private object evaluateDict(DictExpr node)
        {
            Dictionary<object, object> elements = new Dictionary<object, object>();
            foreach (KeyValuePair<IExpr, IExpr> element in node.elements)
            {
                object key = evaluate(element.Key);
                object value = evaluate(element.Value);
                if (elements.ContainsKey(key))
                {
#warning fix dictionary duplicate key
                    //throw error($"Duplicate key found in dictionary literal: '{stringify(key)}'");
                }
                elements[key] = value;
            }
            return elements;
        }
        #endregion

        #region Helpers
        public static string stringify(object obj) //only for output -> final types not IExpr
        {
            if (obj == null) { return "null"; }
            if (obj is string str) { return str; }
            if (obj is double d) { return d.ToString(); }
            if (obj is bool b) { return b.ToString(); }
            if (obj is List<object> list)
            {
                string sl = "[";
                for (int i = 0; i < list.Count; i++)
                {
                    sl += stringify(list[i]);
                    sl += (i < list.Count - 1) ? ", " : "";
                }
                sl += "]";
                return sl;
            }
            if (obj is Dictionary<object, object> dict)
            {
                string sd = "[";
                for (int i = 0; i < dict.Count; i++)
                {
                    sd += $"{stringify(dict.Keys.ElementAt(i))}: {stringify(dict.Values.ElementAt(i))}";
                    sd += (i < dict.Count - 1) ? ", " : "";
                }
                sd += "]";
                return sd;
            }
            return obj.ToString();
        }

        //falsey values: null, false, 0, NaN, "", [] (arrays and dictionaries)
        public static bool isTruthy(object obj)
        {
            if (obj == null) { return false; }
            if (obj is bool b) { return b; }
            if (obj is double d) { return d != 0 && !double.IsNaN(d); }
            if (obj is string s) { return !string.IsNullOrEmpty(s); }
            if (obj is List<object> list) { return list.Count > 0; }
            if (obj is Dictionary<object, object> dict) { return dict.Count > 0; }

            return true;
        }

        //yay c# modulus is truncation not round down -> -7 % 3 = -1
        public static bool isInteger(object obj) { return obj is double d && d % 1 < 0.0000001 && d % 1 > -0.0000001; }

        public static bool isNonNegativeInt(object obj) { return isInteger(obj) && (double) obj >= 0; } //can also be 0

#warning still shallow equlaity -> e.g. list of dicts/list of lists

        public static bool deepEqual(Dictionary<object, object> d1, Dictionary<object, object> d2)
        {
            return d1.SequenceEqual(d2);
        }
        
        public static bool deepEqual(List<object> l1, List<object> l2)
        {
            return l1.SequenceEqual(l2);
        }

        #endregion

        #region Errors and Warnings
        private Diagnostic error(Token t, string message) { return new Diagnostic(t.line, Program.lines[t.line - 1], message, DiagnosticLevel.ERROR); }
        private Diagnostic warning(Token t, string message) { return new Diagnostic(t.line, Program.lines[t.line - 1], message, DiagnosticLevel.WARNING); }
        #endregion
    }
}
