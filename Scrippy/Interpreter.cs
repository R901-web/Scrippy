using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.AccessControl;

namespace Scrippy
{
    public class Interpreter
    {
        public Expr ast { get; }

        public Interpreter(Expr ast)
        {
            this.ast = ast;
        }

        public Value interpretAST()
        {
            try { return evaluate(ast); }
            catch (Diagnostic d) when (d.severity == DiagnosticLevel.ERROR)
            {
                DiagnosticHandler.add(d);
                return null;
            }
        }

        private Value evaluate(Expr expr)
        {
            switch (expr)
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

        private Value evaluateBinary(BinaryExpr binary)
        {
            Token t = binary.op;
            Debug.Assert(t.type == TokenType.Plus || t.type == TokenType.Minus || t.type == TokenType.Div ||
                t.type == TokenType.Mod || t.type == TokenType.Mult || t.type == TokenType.Power ||
                t.type == TokenType.More || t.type == TokenType.MoreEQ || t.type == TokenType.Less || t.type == TokenType.LessEQ ||
                t.type == TokenType.Spaceship || t.type == TokenType.Equal || t.type == TokenType.NotEQ || t.type == TokenType.RefEQ ||
                t.type == TokenType.And || t.type == TokenType.Or || t.type == TokenType.Elvis || t.type == TokenType.NullCoalesce);

            switch (t.type)
            {
                case TokenType.Plus:
                case TokenType.Minus:
                case TokenType.Div:
                case TokenType.Mult:
                case TokenType.Power:
                case TokenType.Mod:
                    return evaluateArithmetic(binary);
                case TokenType.More:
                case TokenType.MoreEQ:
                case TokenType.Less:
                case TokenType.LessEQ:
                case TokenType.Spaceship:
                    return evaluateComparison(binary);
                case TokenType.Equal:
                case TokenType.NotEQ:
                case TokenType.RefEQ:
                    return evaluateEquality(binary);
                case TokenType.And:
                case TokenType.Or:
                    return evaluateLogical(binary);
                case TokenType.Elvis:
                case TokenType.NullCoalesce:
                    return evaluateFallback(binary);
            }
            
            throw new NotImplementedException();
        }

        #region Binary Operators
        private Value evaluateArithmetic(BinaryExpr binary)
        {
            Value left = evaluate(binary.left);
            Value right = evaluate(binary.right);

            switch(binary.op.type)
            {
                case TokenType.Plus:
                    if (left is ArrValue a) { return a + right; }
                    if (left is NumValue nl && right is NumValue nr) { return nl + nr; }
                    if (left is StrValue sl && right is StrValue sr) { return sl + sr; }
                    if (left is DictValue dl && right is DictValue dr) { return dl + dr; }
                    if (right is NullValue) { return left; }
                    throw error(binary, $"Unsupported operand for addition: {left.getTypeName()}, {right.getTypeName()}");
                case TokenType.Minus:
                    if (left is NumValue nl2 && right is NumValue nr2) { return nl2 - nr2; }
                    if (left is StrValue sl2 && right is StrValue sr2) { return sl2 - sr2; }
                    throw error(binary, $"Unsupported operand for subtraction: {left.getTypeName()}, {right.getTypeName()}");
                case TokenType.Mult:
                    if (!(right is NumValue nr3)) { throw error(binary, $"Unsupported operand for multiplication: {left.getTypeName()}, {right.getTypeName()}"); }
                    if (left is DictValue d) { return d * nr3; }
                    if (left is ArrValue al3) { return al3 * nr3; }
                    if (left is NumValue nl3) { return nl3 * nr3; }
                    if (left is StrValue s) { return s * nr3; }
                    if (left is NullValue) { return NullValue.instance; }
                    throw error(binary, $"Unsupported operand for multiplication: {left.getTypeName()}, {right.getTypeName()}");
                case TokenType.Div:
                    if (left is NumValue nl4 && right is NumValue nr4) { return nl4 / nr4; }
                    throw error(binary, $"Unsupported operand for division: {left.getTypeName()}, {right.getTypeName()}");
                case TokenType.Mod:
                    if (left is NumValue nl5 && right is NumValue nr5) { return nl5 % nr5; }
                    throw error(binary, $"Unsupported operand for division: {left.getTypeName()}, {right.getTypeName()}");
                case TokenType.Power: //returns NaN when 0^0, complex nums, etc.
                    if (left is NumValue nl6 && right is NumValue nr6) { return (NumValue) Math.Pow((double) nl6, (double) nr6); }
                    throw error(binary, $"Unsupported operand for exponentiation: {left.getTypeName()}, {right.getTypeName()}");
            }

            return null;
        }
        private Value evaluateComparison(BinaryExpr binary)
        {
            Value left = evaluate(binary.left);
            Value right = evaluate(binary.right);

            switch(binary.op.type)
            {
                case TokenType.Less:
                    return (BoolValue) (left.CompareTo(right) < 0);
                case TokenType.LessEQ:
                    return (BoolValue) (left.CompareTo(right) <= 0);
                case TokenType.More:
                    return (BoolValue) (left.CompareTo(right) > 0);
                case TokenType.MoreEQ:
                    return (BoolValue) (left.CompareTo(right) >= 0);
                case TokenType.Spaceship:
                    return new NumValue(left.CompareTo(right));
            }

            return null;
        }
        private Value evaluateEquality(BinaryExpr binary)
        {
            Value left = evaluate(binary.left);
            Value right = evaluate(binary.right);

            switch(binary.op.type)
            {
                case TokenType.Equal:
                    return (BoolValue) left.Equals(right);
                case TokenType.NotEQ:
                    return (BoolValue) !left.Equals(right);
                case TokenType.RefEQ:
                    if (left is NumValue && right is NumValue) { return (BoolValue) left.Equals(right); } //value types
                    return (BoolValue) ReferenceEquals(left, right); //for bool & null are interned, all bools/nulls point to same
            }

            return null;
        }
        private Value evaluateLogical(BinaryExpr binary) //lazy
        {
            Value left = evaluate(binary.left);

            switch (binary.op.type)
            {
                case TokenType.And:
                    if (!(left is BoolValue || left is NullValue)) { throw error(binary.left, $"Unsupported operand for logical and: {left.getTypeName()}"); }
                    if (left.Equals(BoolValue.falseInstance)) { return BoolValue.falseInstance; }
                    //find right instance if no short circuit
                    Value ra = evaluate(binary.right);
                    if (!(ra is BoolValue || ra is NullValue)) { throw error(binary.right, $"Unsupported operand for logical and: {ra.getTypeName()}"); }
                    //left = true/null, right = null/false/true
                    if (left is BoolValue) { return ra; } //left must be true -> t&t = t, t&f = f, t&n = n
                    else //left = null -> n&t = n, n&f = f, n&n = n
                    {
                        if (ra.Equals(BoolValue.falseInstance)) { return BoolValue.falseInstance; }
                        return NullValue.instance;
                    }
                case TokenType.Or:
                    if (!(left is BoolValue || left is NullValue)) { throw error(binary.left, $"Unsupported operand for logical or: {left.getTypeName()}"); }
                    if (left.Equals(BoolValue.trueInstance)) { return BoolValue.trueInstance; }
                    //find right instance if no short circuit
                    Value ro = evaluate(binary.right);
                    if (!(ro is BoolValue || ro is NullValue)) { throw error(binary.right, $"Unsupported operand for logical or: {ro.getTypeName()}"); }
                    //left = false/null, right = null/false/true
                    if (left is BoolValue) { return ro; } //left must be false -> f|f = f, f|t = t, f|n = n
                    else //left = null -> n|f = n, n|t = t, n|n = n
                    {
                        if (ro.Equals(BoolValue.trueInstance)) { return BoolValue.trueInstance; }
                        return NullValue.instance;
                    }
            }

            return null;
        }
        private Value evaluateFallback(BinaryExpr binary) //lazy
        {
            Value left = evaluate(binary.left);

            switch(binary.op.type)
            {
                case TokenType.NullCoalesce:
                    return !(left is NullValue) ? left : evaluate(binary.right);
                case TokenType.Elvis:
                    return left.isTruthy() ? left : evaluate(binary.right);
            }

            return null;
        }
        #endregion

        private Value evaluateUnary(UnaryExpr unary)
        {
            Token t = unary.op;
            Debug.Assert(t.type == TokenType.Plus || t.type == TokenType.Minus || t.type == TokenType.Not);

            Value right = evaluate(unary.right);
            switch (t.type)
            {
                case TokenType.Plus:
                    if (right is BoolValue || right is NullValue) { throw error(unary.right, $"Unsupported operand type for unary plus: {right.getTypeName()}"); }
                    return right; //unary plus does nothing
                case TokenType.Minus:
                    if (right is NumValue n) { return -n; }
                    if (right is StrValue s) { return -s; }
                    if (right is ArrValue a) { return -a; }
                    throw error(unary.right, $"Unsupported operand type for unary minus: {right.getTypeName()}");
                case TokenType.Not:
                    if (right is BoolValue b) { return !b; }
                    if (right is NullValue) { return NullValue.instance; } //use 3-value logic
                    throw error(unary.right, $"Unsupported operand type for logical not: {right.getTypeName()}");
            }
            throw new NotImplementedException();
        }

        private Value evaluateTernary(TernaryExpr ternary)
        {
            Token main = ternary.mainOp; Token side = ternary.sideOp;
            Debug.Assert(main.type == TokenType.TernCond && side.type == TokenType.Colon ||
                main.type == TokenType.Range && side.type == TokenType.Colon);

            Value condition = evaluate(ternary.left);
            if (main.type == TokenType.TernCond && side.type == TokenType.Colon) //lazy
            {
                return condition.isTruthy() ? evaluate(ternary.mid) : evaluate(ternary.right);
            }
            throw new NotImplementedException($"Add range pls");
        }

        private Value evaluateGrouping(GroupingExpr grouping) { return evaluate(grouping.expr); }

        private Value evaluateLiteral(LiteralExpr literal) 
        { 
            Debug.Assert(literal.value == null || literal.value is double || literal.value is string || literal.value is bool);
            switch (literal.value)
            {
                case null: return NullValue.instance;
                case double d: return new NumValue(d);
                case string s: return new StrValue(s);
                case bool b: return b ? BoolValue.trueInstance : BoolValue.falseInstance;
            }
            throw new NotImplementedException();
        }

        private Value evaluateArray(ArrayExpr array)
        {
            List<Value> values = new List<Value>();
            foreach (Expr elem in array.elements) { values.Add(evaluate(elem)); }
            return new ArrValue(values);
        }

        private Value evaluateDict(DictExpr dict)
        {
            Dictionary<Value, Value> values = new Dictionary<Value, Value>();
            foreach (KeyValuePair<Expr, Expr> kvp in dict.elements)
            {
                Value key = evaluate(kvp.Key);
                Value value = evaluate(kvp.Value);
                if (!key.isHashable()) { throw error(kvp.Key, $"Key {key.ToString()} is not hashable, cannot be used as a dictionary key"); }
                if (values.ContainsKey(key)) { throw error(kvp.Key, $"Duplicate key {key.ToString()} found in dictionary"); }
                values.Add(key, value);
            }
            return new DictValue(values);
        }

        #region Errors and Warnings
        private Diagnostic error(Expr e, string message)
        {
            string[] strings = new string[e.lineEnd - e.lineStart + 1];
            for (int i = 0; i < strings.Length; i++) { strings[i] = Program.lines[e.lineStart + i - 1]; }
            return new Diagnostic(e.lineStart, strings, message, DiagnosticLevel.ERROR);
        }
        private Diagnostic warning(Expr e, string message)
        {
            string[] strings = new string[e.lineEnd - e.lineStart + 1];
            for (int i = 0; i < strings.Length; i++) { strings[i] = Program.lines[e.lineStart + i - 1]; }
            return new Diagnostic(e.lineStart, strings, message, DiagnosticLevel.WARNING);
        }
        #endregion
    }
}
