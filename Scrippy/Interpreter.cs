using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace Scrippy
{
    public class Interpreter
    {
        public class Environment
        {
            public Environment parent { get; }
            public Dictionary<string, Value> values { get; } = new Dictionary<string, Value>();
            public Dictionary<string, Value> constants { get; } = new Dictionary<string, Value>();

            public Environment(Environment parent = null)
            {
                this.parent = parent;                
            }

            public Value this[string key]
            {
                get
                {
                    if (constants.ContainsKey(key)) { return constants[key]; }
                    if (values.ContainsKey(key)) { return values[key]; }
                    if (parent != null) { return parent[key]; }
                    throw new KeyNotFoundException($"Variable {key} has not been declared");
                }
            }

            public void define(string key, Value value)
            {
                if (values.ContainsKey(key)) { throw new Exception($"Variable {key} has already been declared"); }
                if (constants.ContainsKey(key)) { throw new Exception($"Constant {key} has already been declared"); }
                values[key] = value;
            }

            public void assign(string key, Value value)
            {
                if (constants.ContainsKey(key)) { throw new Exception($"Cannot assign to constant {key}"); }
                if (values.ContainsKey(key)) { values[key] = value; return; }
                if (parent != null) { parent.assign(key, value); return; }
                throw new Exception($"Variable {key} has not been declared");
            }

            public void defineConst(string key, Value value)
            {
                if (constants.ContainsKey(key)) { throw new Exception($"Constant {key} has already been declared"); }
                if (values.ContainsKey(key)) { throw new Exception($"Variable {key} has already been declared"); }
                constants[key] = value;
            }
        }

        public Stmt[] program { get; }

        private Environment environment { get; set; }

        public Interpreter(Stmt[] program)
        {
            this.program = program;
            environment = new Environment(); //global scope
        }

        public void interpretAST()
        {
            try { for (int i = 0; i < program.Length; i++) { execute(program[i]); } }
            catch (Diagnostic d) when (d.severity == DiagnosticLevel.ERROR)
            {
                DiagnosticHandler.add(d);
                DiagnosticHandler.reportAll();
            }
        }

        #region Statements
        private void execute(Stmt stmt)
        {
            Debug.Assert(stmt is ExprStmt || stmt is WriteStmt || stmt is VarDeclStmt 
                || stmt is BlockStmt || stmt is ArrDestrStmt || stmt is DictDestrStmt
                || stmt is IfStmt || stmt is WhileStmt || stmt is KeyStmt);

            switch (stmt)
            {
                case ExprStmt e:
                    executeExpr(e);
                    return;
                case WriteStmt w:
                    executeWrite(w);
                    return;
                case VarDeclStmt v:
                    executeVarDecl(v);
                    return;
                case BlockStmt b:
                    executeBlock(b);
                    return;
                case ArrDestrStmt a:
                    executeArrDestr(a);
                    return;
                case DictDestrStmt d:
                    executeDictDestr(d);
                    return;
                case IfStmt i:
                    executeIf(i);
                    return;
                case WhileStmt w:
                    executeWhile(w);
                    return;
                case KeyStmt k:
                    executeKey(k);
                    return;
            }
            throw new NotImplementedException();
        }

        private void executeExpr(ExprStmt stmt)
        {
            evaluate(stmt.expr);
        }

        private void executeWrite(WriteStmt stmt)
        {
            Value v = evaluate(stmt.expr);
            Console.Write(v);
        }

        private void executeBlock(BlockStmt stmt)
        {
            Environment oldEnv = environment;
            environment = new Environment(oldEnv);
            try { foreach (Stmt s in stmt.statements) { execute(s); } }
            finally { environment = oldEnv; }
        }

        private void executeVarDecl(VarDeclStmt stmt)
        {
            Value value = stmt.initialized ? evaluate(stmt.initializer) : null;
            foreach (Token t in stmt.names)
            {
                try 
                { 
                    if (stmt.isConst) { environment.defineConst(t.source, value); }
                    else { environment.define(t.source, value); }
                }
                catch (Exception e) { throw error(stmt, e.Message); }
            }
        }

        private void executeArrDestr(ArrDestrStmt stmt)
        {
            ArrValue v = (ArrValue) evaluate(stmt.initializer); //an ArrValue

            for (int i = 0; i < v.length; i++)
            {
                try
                {
                    if (stmt.isConst) { environment.defineConst(stmt.names[i].source, v[i]); }
                    else { environment.define(stmt.names[i].source, v[i]); }
                }
                catch (Exception e) { throw error(stmt, e.Message); }
            }
        }

        private void executeDictDestr(DictDestrStmt stmt)
        {
            DictValue v = (DictValue) evaluate(stmt.initializer);

            foreach(KeyValuePair<Token, Expr> kvp in stmt.names)
            {
                Token varName = kvp.Key;
                Value dictName = evaluate(kvp.Value);
                try
                {
                    if (stmt.isConst) { environment.defineConst(varName.source, v[dictName]); }
                    else { environment.define(varName.source, v[dictName]); }
                }
                catch (KeyNotFoundException) { throw error(stmt, $"Key not found in dictionary destructuring: {dictName}"); }
                catch (Exception e) { throw error(stmt, e.Message); }
            }
        }

        private void executeIf(IfStmt stmt)
        {
            Value v = evaluate(stmt.condition);
            if (!(v is BoolValue b)) { throw error(stmt, $"Boolean value required for if statment, got {v.getTypeName()}"); }

            if ((bool) b)
            {
                execute(stmt.ifBranch);
            }
            else if (stmt.elseBranch != null)
            {
                execute(stmt.elseBranch);
            }
        }

        private void executeWhile(WhileStmt stmt)
        {
            while (true)
            {
                Value v = evaluate(stmt.condition);
                if (!(v is BoolValue b))
                {
                    throw error(stmt, $"Boolean value required for while statment, got {v.getTypeName()}");
                }
                if (!(bool) b) { break; }
                else 
                {
                    try { execute(stmt.body); }
                    catch(Diagnostic d) when (d.Message == "Break keyword outside of loop" || d.Message == "Continue keyword outside of loop")
                    {
                        if (d.Message == "Continue keyword outside of loop") { continue; }
                        else if (d.Message == "Break keyword outside of loop") { break; }
                    }
                }
            }
        }

        private void executeKey(KeyStmt stmt)
        {
            if (stmt.keyword.type == TokenType.Break) { throw error(stmt, "Break keyword outside of loop"); }
            else if (stmt.keyword.type == TokenType.Continue) { throw error(stmt, "Continue keyword outside of loop"); }
        }
        #endregion

        #region Expressions

        private Value evaluate(Expr expr)
        {
            Debug.Assert(expr is UnaryExpr || expr is BinaryExpr || expr is TernaryExpr ||
                expr is LiteralExpr || expr is ArrayExpr || expr is DictExpr ||
                expr is GroupingExpr || expr is VarExpr || expr is AssignExpr || expr is IncrExpr ||
                expr is ReadExpr || expr is BlockExpr);

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
                case VarExpr var:
                    return evaluateVar(var);
                case AssignExpr assign:
                    return evaluateAssign(assign);
                case IncrExpr incr:
                    return evaluateIncr(incr);
                case ReadExpr read:
                    return evaluateRead(read);
                case BlockExpr block:
                    return evaluateBlock(block);
            }

            throw new NotImplementedException();
        }

        private Value evaluateBinary(BinaryExpr binary)
        {
            Token t = binary.op;
            Debug.Assert(t.type == TokenType.Plus || t.type == TokenType.Minus || t.type == TokenType.Div ||
                t.type == TokenType.Mod || t.type == TokenType.Mult || t.type == TokenType.Power ||
                t.type == TokenType.More || t.type == TokenType.MoreEQ || t.type == TokenType.Less || t.type == TokenType.LessEQ ||t.type == TokenType.Spaceship ||
                t.type == TokenType.Equal || t.type == TokenType.NotEQ || t.type == TokenType.RefEQ || t.type == TokenType.Match || t.type == TokenType.NotMatch ||
                t.type == TokenType.And || t.type == TokenType.Or || t.type == TokenType.Elvis || t.type == TokenType.NullCoalesce);

            try
            {
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
                    case TokenType.Match:
                    case TokenType.NotMatch:
                        return evaluateEquality(binary);
                    case TokenType.And:
                    case TokenType.Or:
                        return evaluateLogical(binary);
                    case TokenType.Elvis:
                    case TokenType.NullCoalesce:
                        return evaluateFallback(binary);
                }
            }
            catch (Exception e) when (!(e is Diagnostic)) { throw error(binary, e.Message); }

            throw new NotImplementedException();
        }

        #region Binary Operators
        private Value evaluateArithmetic(BinaryExpr binary)
        {
            Value left = evaluate(binary.left);
            Value right = evaluate(binary.right);

            switch (binary.op.type)
            {
                case TokenType.Plus:
                    if (left is ArrValue a) { return a + right; }
                    if (left is NumValue nl && right is NumValue nr) { return nl + nr; }
                    if (left is StrValue sl) { return sl + right; }
                    if (left is DictValue dl && right is DictValue dr) { return dl + dr; }
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

            switch (binary.op.type)
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

            switch (binary.op.type)
            {
                case TokenType.Equal:
                    return (BoolValue) left.Equals(right);
                case TokenType.NotEQ:
                    return (BoolValue) !left.Equals(right);
                case TokenType.RefEQ:
                    if (left is NumValue && right is NumValue) { return (BoolValue) left.Equals(right); } //value types
                    return (BoolValue) ReferenceEquals(left, right); //for bool & null are interned, all bools/nulls point to same
                case TokenType.Match:
                    if (!(right is TypeValue t)) { throw error(binary, "Right operand must be a type value"); }
                    return (BoolValue) new TypeValue(left).Equals(right);
                case TokenType.NotMatch:
                    if (!(right is TypeValue t2)) { throw error(binary, "Right operand must be a type value"); }
                    return (BoolValue) !new TypeValue(left).Equals(right);
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

            switch (binary.op.type)
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


        private Value evaluateAssign(AssignExpr assign)
        {
            Value newValue = evaluate(assign.newValue);
            environment.assign(assign.name.source, newValue);
            return newValue;
        }

        private Value evaluateIncr(IncrExpr incr)
        {
            Token t = incr.incrType;
            Debug.Assert(t.type == TokenType.Increment || t.type == TokenType.Decrement);
            string varName = incr.name.source;
            Value name = environment[varName];

            if (incr.isPost)
            {
                switch(t.type)
                {
                    case TokenType.Increment:
                        if (name is NumValue n) { n++; environment.assign(varName, n); return name; }
                        throw error(incr, $"Unsupported type for postfix increment: {name.getTypeName()}");
                    case TokenType.Decrement:
                        if (name is NumValue n2) { n2--; environment.assign(varName, n2); return name; }
                        else if (name is StrValue s2) { s2--; environment.assign(varName, s2); return name; }
                        else if (name is ArrValue a2) { Value orig = a2.clone(); a2--; environment.assign(varName, a2); return orig; }
                        throw error(incr, $"Unsupported type for postfix decrement: {name.getTypeName()}");
                }
                throw new NotImplementedException();
            }
            else
            {
                switch (t.type)
                {
                    case TokenType.Increment:
                        if (name is NumValue n) { ++n; environment.assign(varName, n); return n; }
                        throw error(incr, $"Unsupported type for postfix increment: {name.getTypeName()}");
                    case TokenType.Decrement:
                        if (name is NumValue n2) { --n2; environment.assign(varName, n2); return n2; }
                        else if (name is StrValue s2) { --s2; environment.assign(varName, s2); return s2; }
                        else if (name is ArrValue a2) { --a2; environment.assign(varName, a2); return a2; }
                        throw error(incr, $"Unsupported type for postfix decrement: {name.getTypeName()}");
                }
                throw new NotImplementedException();
            }
        }

        private Value evaluateTernary(TernaryExpr ternary)
        {
            Token main = ternary.mainOp; Token side = ternary.sideOp;
            Debug.Assert((main.type == TokenType.TernCond && side.type == TokenType.Colon) ||
                (main.type == TokenType.Range && side.type == TokenType.Colon));

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
            Debug.Assert(literal.value == null || literal.value is double || literal.value is string || literal.value is bool || literal.value is Type);
            switch (literal.value)
            {
                case null: return NullValue.instance;
                case double d: return new NumValue(d);
                case string s: return new StrValue(s);
                case bool b: return b ? BoolValue.trueInstance : BoolValue.falseInstance;
                case Type t: return new TypeValue(t);
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

        private Value evaluateVar(VarExpr var)
        {
            try
            {
                Value v = environment[var.name.source];
                if (v == null) { throw error(var, $"Variable {var.name.source} has not been initialized"); }
                return v;
            }
            catch (KeyNotFoundException) { throw error(var, $"Variable {var.name.source} has not been declared"); }
        }

        private Value evaluateRead(ReadExpr read)
        {
            string input = Console.ReadLine();
            return new StrValue(input);
        }

        private Value evaluateBlock(BlockExpr block)
        {
            Environment oldEnv = environment;
            environment = new Environment(oldEnv);
            try
            {
                foreach (Stmt s in block.statements) { execute(s); }
                return evaluate(block.last);
            }
            finally { environment = oldEnv; }
        }
        #endregion

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

        private Diagnostic error(Stmt s, string message)
        {
            string[] strings = new string[s.lineEnd - s.lineStart + 1];
            for (int i = 0; i < strings.Length; i++) { strings[i] = Program.lines[s.lineStart + i - 1]; }
            return new Diagnostic(s.lineStart, strings, message, DiagnosticLevel.ERROR);
        }
        private Diagnostic warning(Stmt s, string message)
        {
            string[] strings = new string[s.lineEnd - s.lineStart + 1];
            for (int i = 0; i < strings.Length; i++) { strings[i] = Program.lines[s.lineStart + i - 1]; }
            return new Diagnostic(s.lineStart, strings, message, DiagnosticLevel.WARNING);
        }
        #endregion
    }
}
