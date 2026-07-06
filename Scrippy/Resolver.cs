using System;
using System.Collections.Generic;

namespace Scrippy
{
    public class Resolver
    {
        public Stmt[] program { get; }

        private Stack<Dictionary<string, (bool isConst, bool defined)>> scopes;

        private int loopDepth;
        private int methodDepth;

        public Resolver(Stmt[] program)
        {
            this.program = program;
            scopes = new Stack<Dictionary<string, (bool, bool)>>();
        }

#warning make it return the side table? -> pass to interpreter
        public void resolveAST() { foreach (Stmt s in program) { resolve(s); } }

        #region Statements
        private void resolve(Stmt stmt)
        {
            switch (stmt)
            {
                case BlockStmt b: resolveBlock(b); return;
                case VarDeclStmt v: resolveVarDecl(v); return;
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
        }


        #endregion

        #region Expressions
        private void resolve(Expr expr)
        {
            switch (expr)
            {
                case VarExpr v: resolveVar(v); return;
            }
            throw new NotImplementedException();
        }

        private void resolveVar(VarExpr expr)
        {

        }

        #endregion

        #region Environment
        private void declare(Token name, bool isConst)
        {
            Dictionary<string, (bool isConst, bool defined)> scope = scopes.Peek();
            if (scope.ContainsKey(name.source)) { throw new Exception($"{(scope[name.source].isConst ? "Constant" : "Variable")} {name.source} has already been declared"); }
            scope[name.source] = (isConst, false);
        }

        private void define(Token name, bool isConst)
        {
            Dictionary<string, (bool isConst, bool defined)> scope = scopes.Peek();
            if (!scope.ContainsKey(name.source)) { throw new Exception($"{(scope[name.source].isConst ? "Constant" : "Variable")} {name.source} has not been declared"); }
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
