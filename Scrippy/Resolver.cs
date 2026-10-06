using System;
using System.Collections.Generic;
using System.Linq;

namespace Scrippy
{
    public class Resolver
    {
        public Stmt[] program { get; }

        private Stack<Dictionary<string, (bool isConst, bool defined)>> scopes;
        private Dictionary<Token, int> depthMap;

        private int loopDepth;
        private int funcDepth;

        public Resolver(Stmt[] program)
        {
            this.program = program;
            scopes = new Stack<Dictionary<string, (bool, bool)>>();
            scopes.Push(new Dictionary<string, (bool isConst, bool defined)>()); //global scope
            depthMap = new Dictionary<Token, int>();

            scopes.Peek()["write"] = (true, true);
            scopes.Peek()["clone"] = (true, true);
            scopes.Peek()["read"] = (true, true);
            scopes.Peek()["rand"] = (true, true);
        }

        public Dictionary<Token, int> resolveAST()
        {
            foreach (Stmt s in program) { resolve(s); }
            return depthMap;
        }

        #region Statements
        private void resolve(Stmt stmt)
        {
            switch (stmt)
            {
                case BlockStmt b: resolveBlock(b); return;
                case VarDeclStmt v: resolveVarDecl(v); return;
                case FuncDeclStmt f: resolveFuncDecl(f); return;
                case ExprStmt e: resolveExprStmt(e); return;
                case ArrDestrStmt a: resolveArrDestr(a); return;
                case DictDestrStmt d: resolveDictDestr(d); return;
                case IfStmt i: resolveIf(i); return;
                case WhileStmt w: resolveWhile(w); return;
                case JumpStmt j: resolveJump(j); return;
            }
            throw new NotImplementedException();
        }

        private void resolveBlock(BlockStmt stmt)
        {
            scopes.Push(new Dictionary<string, (bool, bool)>());
            foreach (Stmt s in stmt.statements) { resolve(s); }
            scopes.Pop();
        }

        private void resolveVarDecl(VarDeclStmt stmt)
        {
            try { foreach (Token t in stmt.names) { declare(t, stmt.isConst); } }
            catch (Exception e) { error(stmt, e.Message); }
            if (stmt.initialized) { resolve(stmt.initializer); }
            try { foreach (Token t in stmt.names) { define(t, stmt.isConst); } }
            catch (Exception e) { error(stmt, e.Message); }

            if (stmt.isConst && !stmt.initialized) { error(stmt, "Constant variables must be initialized"); }
        }

        private void resolveArrDestr(ArrDestrStmt stmt)
        {
            try 
            { 
                foreach (Token t in stmt.names) { declare(t, stmt.isConst); } 
                if (stmt.variadic != null) { declare(stmt.variadic.Value, stmt.isConst); }
            }
            catch (Exception e) { error(stmt, e.Message); }
            resolve(stmt.initializer);
            try 
            { 
                foreach (Token t in stmt.names) { define(t, stmt.isConst); }
                if (stmt.variadic != null) { define(stmt.variadic.Value, stmt.isConst); }
            }
            catch (Exception e) { error(stmt, e.Message); }
        }

        private void resolveDictDestr(DictDestrStmt stmt)
        {
            try { foreach (KeyValuePair<Token, Expr> kvp in stmt.names) { declare(kvp.Key, stmt.isConst); resolve(kvp.Value); } }
            catch (Exception e) { error(stmt, e.Message); }
            resolve(stmt.initializer);
            try { foreach (KeyValuePair<Token, Expr> kvp in stmt.names) { define(kvp.Key, stmt.isConst); resolve(kvp.Value); } }
            catch (Exception e) { error(stmt, e.Message); }

            foreach (KeyValuePair<Token, Expr> kvp in stmt.names)
            {
                if (kvp.Key.type == TokenType.Underscore) { warning(stmt, "Underscores in dictionary destructuring will be ignored"); }
            }
        }

        private void resolveIf(IfStmt stmt)
        {
            resolve(stmt.condition);
            resolve(stmt.ifBranch);
            if (stmt.elseBranch != null) { resolve(stmt.elseBranch); }
        }

        private void resolveWhile(WhileStmt stmt)
        {
            loopDepth++;
            resolve(stmt.condition);
            resolve(stmt.body);
            if (stmt.change != null) { resolve(stmt.change); }
            loopDepth--;
        }

        private void resolveJump(JumpStmt stmt)
        {
            TokenType type = stmt.keyword.type;

            if (type == TokenType.Return)
            {
                if (funcDepth == 0) { if (stmt.value != null) { error(stmt, "Top-level returns cannot return values"); } }
                if (stmt.value != null) { resolve(stmt.value); }
            }
            if (type == TokenType.Break || type == TokenType.Continue)
            {
                if (loopDepth == 0) { error(stmt, "Break or continue must be in a loop"); }
            }
        }

        private void resolveFuncDecl(FuncDeclStmt stmt)
        {
            try
            {
                declare(stmt.name, true); //the function is constant but parameters are not
                define(stmt.name, true); //declare early for recursion + will have a body
            }
            catch (Exception e) { error(stmt, e.Message); }

            scopes.Push(new Dictionary<string, (bool, bool)>());
            foreach (Token t in stmt.param)
            {
                try 
                { 
                    declare(t, false); define(t, false); 
                    if (stmt.defValues.TryGetValue(t, out Expr def)) { resolve(def); }
                }
                catch (Exception e) { error(stmt, e.Message); }
            }
            if (stmt.variadic != null)
            {
                try { declare((Token) stmt.variadic, false); define((Token) stmt.variadic, false); }
                catch (Exception e) { error(stmt, e.Message); }
            }
            funcDepth++;
            foreach (Stmt s in stmt.body) { resolve(s); }
            funcDepth--;
            scopes.Pop();
        }

        private void resolveExprStmt(ExprStmt stmt) { resolve(stmt.expr); }
        #endregion

        #region Expressions
        private void resolve(Expr expr)
        {
            switch (expr)
            {
                case VarExpr v: resolveVar(v); return;
                case AssignExpr a: resolveAssign(a); return;
                case IncrExpr i: resolveIncr(i); return;
                case FuncExpr f: resolveFunc(f); return;
                case BlockExpr b: resolveBlock(b); return;
                case BinaryExpr bin: resolveBinary(bin); return;
                case GroupingExpr g: resolveGrouping(g); return;
                case UnaryExpr u: resolveUnary(u); return;
                case LiteralExpr l: resolveLiteral(l); return;
                case TernaryExpr t: resolveTernary(t); return;
                case ArrayExpr a: resolveArray(a); return;
                case DictExpr d: resolveDict(d); return;
                case CallExpr c: resolveCall(c); return;
                case IndexExpr i2: resolveIndex(i2); return;
            }
            throw new NotImplementedException();
        }

        private void resolveVar(VarExpr expr)
        {
            if (scopes.Count != 0 && scopes.Peek().TryGetValue(expr.name.source, out (bool isConst, bool defined) info) && !info.defined) { error(expr, "Cannot read variable in its own initializer"); }
            try { int depth = find(expr.name.source); depthMap[expr.name] = depth; }
            catch (Exception e) { error(expr, e.Message); }
        }

        private void resolveAssign(AssignExpr expr)
        {
            resolve(expr.name);
            resolve(expr.newValue);

            if (expr.name is VarExpr v)
            {
                try
                {
                    bool isConst = lookup(v.name.source).isConst;
                    if (isConst) { error(expr, $"Cannot assign to constant {v.name.source}"); }
                }
                catch (Exception e) { error(v, e.Message); }
            }
            else if (expr.name is IndexExpr i)
            {
                resolve(i.obj);
                resolve(i.index);
            }
            else { error(expr, "Assignment can only be used on variables"); }
        }

        private void resolveIncr(IncrExpr expr)
        {
            resolve(expr.name);

            if (expr.name is VarExpr v)
            {
                try
                {
                    bool isConst = lookup(v.name.source).isConst;
                    if (isConst) { error(expr, $"Cannot increment or decrement constant {v.name.source}"); }
                }
                catch (Exception e) { error(v, e.Message); }
            }
            else if (expr.name is IndexExpr i)
            {
                resolve(i.obj);
                resolve(i.index);
            }
            else { error(expr, "Increment and decrement can only be used on variables"); }
        }

        private void resolveFunc(FuncExpr expr)
        {
            scopes.Push(new Dictionary<string, (bool, bool)>());
            foreach (Token t in expr.param)
            {
                try { declare(t, false); define(t, false); }
                catch (Exception e) { error(expr, e.Message); }
            }
            funcDepth++;
            foreach (Stmt s in expr.body) { resolve(s); }
            funcDepth--;
            scopes.Pop();
        }

        private void resolveBlock(BlockExpr expr)
        {
            scopes.Push(new Dictionary<string, (bool, bool)>());
            foreach (Stmt s in expr.statements) { resolve(s); }
            resolve(expr.last);
            scopes.Pop();
        }

        private void resolveBinary(BinaryExpr expr)
        {
            resolve(expr.left);
            resolve(expr.right);
        }

        private void resolveTernary(TernaryExpr expr)
        {
            resolve(expr.left);
            resolve(expr.mid);
            resolve(expr.right);
        }

        private void resolveGrouping(GroupingExpr expr) { resolve(expr.expr); }

        private void resolveUnary(UnaryExpr expr) { resolve(expr.right); }

        private void resolveLiteral(LiteralExpr expr) { }

        private void resolveArray(ArrayExpr expr) { foreach (Expr e in expr.elements) { resolve(e); } }

        private void resolveDict(DictExpr expr) { foreach (KeyValuePair<Expr, Expr> kvp in expr.elements) { resolve(kvp.Key); resolve(kvp.Value); } }

        private void resolveCall(CallExpr expr)
        {
            resolve(expr.caller);
            foreach (Expr e in expr.arguments) { resolve(e); }
        }

        private void resolveIndex(IndexExpr expr)
        {
            resolve(expr.obj);
            resolve(expr.index);
        }
        #endregion

        #region Environment
        private int find(string name)
        {
            for (int i = 0; i < scopes.Count; i++) //goes from top to bottom
            {
                Dictionary<string, (bool isConst, bool defined)> scope = scopes.ElementAt(i);
                if (scope.ContainsKey(name)) { return i; }
            }
            throw new Exception($"Variable {name} has not been declared");
        }

        private (bool isConst, bool defined) lookup(string name)
        {
            for (int i = 0; i < scopes.Count; i++) //goes from top to bottom -> outer scopes shadow inner
            {
                Dictionary<string, (bool isConst, bool defined)> scope = scopes.ElementAt(i);
                if (scope.ContainsKey(name)) { return scope[name]; }
            }
            throw new Exception($"Variable {name} has not been declared");
        }

        private void declare(Token name, bool isConst)
        {
            Dictionary<string, (bool isConst, bool defined)> scope = scopes.Peek();
            if (scope.ContainsKey(name.source)) { throw new Exception($"{(scope[name.source].isConst ? "Constant" : "Variable")} {name.source} has already been declared"); }
            scope[name.source] = (isConst, false);
        }

        private void define(Token name, bool isConst)
        {
            Dictionary<string, (bool isConst, bool defined)> scope = scopes.Peek();
            if (!scope.ContainsKey(name.source)) { throw new Exception($"Variable {name.source} has not been declared"); }
            scope[name.source] = (isConst, true);
        }
        #endregion

        #region Warnings and Errors
        private void error(Expr e, string message = "")
        {
            int lineStart = e.lineStart;
            int lineEnd = e.lineEnd;
            string[] strings = new string[lineEnd - lineStart + 1];
            for (int i = 0; i < strings.Length; i++) { strings[i] = Program.lines[lineStart + i - 1]; }
            DiagnosticHandler.add(new Diagnostic(lineStart, strings, message, DiagnosticLevel.ERROR));
        }

        private void error(Stmt s, string message = "")
        {
            int lineStart = s.lineStart;
            int lineEnd = s.lineEnd;
            string[] strings = new string[lineEnd - lineStart + 1];
            for (int i = 0; i < strings.Length; i++) { strings[i] = Program.lines[lineStart + i - 1]; }
            DiagnosticHandler.add(new Diagnostic(lineStart, strings, message, DiagnosticLevel.ERROR));
        }

        private void warning(Expr e, string message = "")
        {
            int lineStart = e.lineStart;
            int lineEnd = e.lineEnd;
            string[] strings = new string[lineEnd - lineStart + 1];
            for (int i = 0; i < strings.Length; i++) { strings[i] = Program.lines[lineStart + i - 1]; }
            DiagnosticHandler.add(new Diagnostic(lineStart, strings, message, DiagnosticLevel.WARNING));
        }

        private void warning(Stmt s, string message = "")
        {
            int lineStart = s.lineStart;
            int lineEnd = s.lineEnd;
            string[] strings = new string[lineEnd - lineStart + 1];
            for (int i = 0; i < strings.Length; i++) { strings[i] = Program.lines[lineStart + i - 1]; }
            DiagnosticHandler.add(new Diagnostic(lineStart, strings, message, DiagnosticLevel.WARNING));
        }
        #endregion
    }
}
