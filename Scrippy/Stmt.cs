using System;
using System.Collections.Generic;

namespace Scrippy
{
    public abstract class Stmt : IEquatable<Stmt>
    {
        public int lineStart { get; }
        public int lineEnd { get; }

        protected Stmt(int lineStart, int lineEnd)
        {
            this.lineStart = lineStart;
            this.lineEnd = lineEnd;
        }

        public abstract bool Equals(Stmt other);
        public override bool Equals(object obj) { return obj is Expr o && Equals(o); }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = (hash * 31) + GetType().GetHashCode();
            return hash;
        }

    }

    /* STATEMENT TYPES
     * ExprStmt
     * VarDeclStmt
     * BlockStmt
     * ArrDestrStmt
     * DictDestrStmt
     * IfStmt
     * WhileStmt
     * KeyStmt
     * FuncDeclStmt
     * ReturnStmt
     */

    public class ExprStmt : Stmt
    {
        public Expr expr { get; }
        public ExprStmt(Expr expr, int lineEnd) : base(expr.lineStart, lineEnd) //lineEnd from ;
        {
            this.expr = expr;
        }
        public override bool Equals(Stmt other)
        {
            if (!(other is ExprStmt e)) { return false; }
            return expr.Equals(e.expr);
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + expr.GetHashCode();
            return hash;
        }
    }

    public class VarDeclStmt : Stmt
    {
        public Token[] names { get; }
        public Expr initializer { get; }
        public bool initialized
        {
            get { return initializer != null; }
        }
        public bool isConst { get; }

        public VarDeclStmt(Token[] names, Expr initializer, bool isConst, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.names = names;
            this.initializer = initializer;
            this.isConst = isConst;
        }
        public override bool Equals(Stmt other)
        {
            if (!(other is VarDeclStmt v)) { return false; }
            if (names.Length != v.names.Length || isConst != v.isConst) { return false; }
            for (int i = 0; i < names.Length; i++) { if (names[i].source != v.names[i].source) { return false; } }
            return initialized ? initializer.Equals(v.initializer) : v.initializer == null;
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + isConst.GetHashCode();
            foreach (Token t in names) { hash = (hash * 31) + t.source.GetHashCode(); }
            hash = (hash * 31) + (initialized ? initializer.GetHashCode() : 0);
            return hash;
        }
    }

    public class ArrDestrStmt : Stmt
    {
        public Token[] names { get; }
        public Expr initializer { get; }
        public bool isConst { get; }

        public ArrDestrStmt(Token[] names, Expr initializer, bool isConst, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.names = names;
            this.isConst = isConst;
            this.initializer = initializer;
        }
        public override bool Equals(Stmt other)
        {
            if (!(other is ArrDestrStmt a)) { return false; }
            if (names.Length != a.names.Length || isConst != a.isConst) { return false; }
            for (int i = 0; i < names.Length; i++) { if (names[i].source != a.names[i].source) { return false; } }
            return initializer.Equals(a.initializer);
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + isConst.GetHashCode();
            foreach (Token t in names) { hash = (hash * 31) + t.source.GetHashCode(); }
            hash = (hash * 31) + initializer.GetHashCode();
            return hash;
        }
    }

    public class DictDestrStmt : Stmt
    {
        private Dictionary<Token, Expr> nm;
        public IReadOnlyDictionary<Token, Expr> names { get { return nm; } }
        public Expr initializer { get; }
        public bool isConst { get; }

        public DictDestrStmt(Dictionary<Token, Expr> nm, Expr initializer, bool isConst, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.nm = nm;
            this.initializer = initializer;
            this.isConst = isConst;
        }
        public override bool Equals(Stmt other)
        {
            if (!(other is DictDestrStmt d)) { return false; }
            if (names.Count != d.names.Count || isConst != d.isConst) { return false; }
            foreach (KeyValuePair<Token, Expr> kvp in names)
            {
                if (!d.names.TryGetValue(kvp.Key, out Expr otherVal)) { return false; }
                if (!kvp.Value.Equals(otherVal)) { return false; }
            }
            return true;
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + isConst.GetHashCode();
            foreach (KeyValuePair<Token, Expr> kvp in names)
            {
                int pairHash = 17;
                pairHash = (pairHash * 31) + kvp.Key.source.GetHashCode();
                pairHash = (pairHash * 31) + kvp.Value.GetHashCode();
                hash ^= pairHash;
            }
            hash = (hash * 31) + initializer.GetHashCode();
            return hash;
        }
    }

    public class BlockStmt : Stmt
    {
        public Stmt[] statements { get; }

        public BlockStmt(Stmt[] statements, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.statements = statements;
        }
        public override bool Equals(Stmt other)
        {
            if (!(other is BlockStmt b)) { return false; }
            if (statements.Length != b.statements.Length) { return false; }
            for (int i = 0; i < statements.Length; i++) { if (!statements[i].Equals(b.statements[i])) { return false; } }
            return true;
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            foreach (Stmt s in statements) { hash = (hash * 31) + s.GetHashCode(); }
            return hash;
        }
    }

    public class IfStmt : Stmt
    {
        public Expr condition { get; }
        public Stmt ifBranch { get; }
        public Stmt elseBranch { get; }

        public IfStmt(Expr condition, Stmt ifBranch, Stmt elseBranch, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.condition = condition;
            this.ifBranch = ifBranch;
            this.elseBranch = elseBranch;
        }
        public override bool Equals(Stmt other)
        {
            if (!(other is IfStmt i)) { return false; }
            if (!(condition.Equals(i.condition) || ifBranch.Equals(i.ifBranch))) { return false; }
            return elseBranch == null ? i.elseBranch == null : elseBranch.Equals(i.elseBranch);
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + condition.GetHashCode();
            hash = (hash * 31) + ifBranch.GetHashCode();
            hash = (hash * 31) + (elseBranch == null ? 0 : elseBranch.GetHashCode());
            return hash;
        }
    }

    public class WhileStmt : Stmt
    {
        public Expr condition { get; }
        public Stmt body { get; }

        public WhileStmt(Expr condition, Stmt body, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.condition = condition;
            this.body = body;
        }
        public override bool Equals(Stmt other)
        {
            if (!(other is WhileStmt w)) { return false; }
            return condition.Equals(w.condition) && body.Equals(w.body);
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + condition.GetHashCode();
            hash = (hash * 31) + body.GetHashCode();
            return hash;
        }
    }

    public class KeyStmt : Stmt
    {
        public Token keyword { get; }

        public KeyStmt(Token keyword, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.keyword = keyword;
        }
        public override bool Equals(Stmt other)
        {
            if (!(other is KeyStmt k)) { return false; }
            return keyword.type == k.keyword.type;
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + keyword.type.GetHashCode();
            return hash;
        }
    }

    public class FuncDeclStmt : Stmt
    {
        public Token name { get; }
        public Token[] param { get; }
        public Stmt[] body { get; }

        public FuncDeclStmt(Token name, Token[] param, Stmt[] body, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.name = name;
            this.param = param;
            this.body = body;
        }
        public override bool Equals(Stmt other)
        {
            if (!(other is FuncDeclStmt f)) { return false; }
            if (name.source != f.name.source || param.Length != f.param.Length || body.Length != f.body.Length) { return false; }
            for (int i = 0; i < param.Length; i++) { if (param[i].source != f.param[i].source) { return false; } }
            for (int i = 0; i < body.Length; i++) { if (!body[i].Equals(f.body[i])) { return false; } }
            return true;
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + name.source.GetHashCode();
            foreach (Token t in param) { hash = (hash * 31) + t.source.GetHashCode(); }
            foreach (Stmt s in body) { hash = (hash * 31) + s.GetHashCode(); }
            return hash;
        }
    }

    public class ReturnStmt : Stmt
    {
        public Expr value { get; }

        public ReturnStmt(Expr value, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.value = value;
        }
        public override bool Equals(Stmt other)
        {
            if (!(other is ReturnStmt r)) { return false; }
            return value == null ? r.value == null : value.Equals(r.value);
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + (value == null ? 0 : value.GetHashCode());
            return hash;
        }
    }
}
