using System;
using System.Collections.Generic;

namespace Scrippy
{
    public abstract class Expr : IEquatable<Expr>
    {
        public int lineStart { get; }
        public int lineEnd { get; }

        protected Expr(int lineStart, int lineEnd)
        {
            this.lineStart = lineStart;
            this.lineEnd = lineEnd;
        }

        public abstract bool Equals(Expr other);
        public override bool Equals(object obj) { return obj is Expr o && Equals(o); }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = (hash * 31) + GetType().GetHashCode();
            return hash;
        }
    }

    /* EXPRESSION TYPES
     * BinaryExpr
     * GroupingExpr
     * UnaryExpr
     * LiteralExpr 
     * TernaryExpr
     * ArrayExpr
     * DictExpr
     * VarExpr
     * AssignExpr
     * IncrExpr
     * BlockExpr
     * CallExpr
     * FuncExpr
     */

    public class BinaryExpr : Expr
    {
        public Expr left { get; }
        public Token op { get; }
        public Expr right { get; }
        public BinaryExpr(Expr left, Token op, Expr right) : base(left.lineStart, right.lineEnd)
        {
            this.left = left;
            this.op = op;
            this.right = right;
        }
        public override bool Equals(Expr other)
        {
            if (!(other is BinaryExpr b)) { return false; }
            return op.type == b.op.type && Equals(left, b.left) && Equals(right, b.right);
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + op.type.GetHashCode();
            hash = (hash * 31) + left.GetHashCode();
            hash = (hash * 31) + right.GetHashCode();
            return hash;
        }
    }

    public class GroupingExpr : Expr
    {
        public Expr expr { get; }
        public GroupingExpr(Expr expr, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.expr = expr;
        }
        public override bool Equals(Expr other)
        {
            if (!(other is GroupingExpr g)) { return false; }
            return Equals(expr, g.expr);
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + expr.GetHashCode();
            return hash;
        }
    }

    public class LiteralExpr : Expr
    {
        public object value { get; }
        public LiteralExpr(object value, int line) : this(value, line, line) { }
        public LiteralExpr(object value, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.value = value;
        }
        public override bool Equals(Expr other)
        {
            if (!(other is LiteralExpr l)) { return false; }
            return Equals(l.value, value);
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + (value?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public class UnaryExpr : Expr
    {
        public Token op { get; }
        public Expr right { get; }
        public UnaryExpr(Token op, Expr right) : base(op.lineStart, right.lineEnd)
        {
            this.op = op;
            this.right = right;
        }
        public override bool Equals(Expr other)
        {
            if (!(other is UnaryExpr u)) { return false; }
            return op.type == u.op.type && right.Equals(u.right);
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + op.type.GetHashCode();
            hash = (hash * 31) + right.GetHashCode();
            return hash;
        }
    }

    public class TernaryExpr : Expr
    {
        public Expr left { get; }
        public Token mainOp { get; }
        public Expr mid { get; }
        public Token sideOp { get; }
        public Expr right { get; }
        public TernaryExpr(Expr left, Token mainOp, Expr mid, Token sideOp, Expr right) : base(left.lineStart, right.lineEnd)
        {
            this.left = left;
            this.mainOp = mainOp;
            this.mid = mid;
            this.sideOp = sideOp;
            this.right = right;
        }
        public override bool Equals(Expr other)
        {
            if (!(other is TernaryExpr t)) { return false; }
            return left.Equals(t.left) && mainOp.type == t.mainOp.type && mid.Equals(t.mid) && sideOp.type == t.sideOp.type && right.Equals(t.right);
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + mainOp.type.GetHashCode();
            hash = (hash * 31) + sideOp.type.GetHashCode();
            hash = (hash * 31) + left.GetHashCode();
            hash = (hash * 31) + mid.GetHashCode();
            hash = (hash * 31) + right.GetHashCode();
            return hash;
        }
    }

    public class ArrayExpr : Expr
    {
        private List<Expr> elem;
        public IReadOnlyList<Expr> elements { get { return elem; } }

        public ArrayExpr(List<Expr> items, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.elem = items;
        }
        public override bool Equals(Expr other)
        {
            if (!(other is ArrayExpr a)) { return false; }
            if (elem.Count != a.elem.Count) { return false; }
            for (int i = 0; i < elem.Count; i++) { if (!elem[i].Equals(a.elem[i])) { return false; } }
            return true;
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            foreach (Expr e in elements) { hash = (hash * 31) + e.GetHashCode(); }
            return hash;
        }
    }

    public class DictExpr : Expr
    {
        private Dictionary<Expr, Expr> elem;
        public IReadOnlyDictionary<Expr, Expr> elements { get { return elem; } }

        public DictExpr(Dictionary<Expr, Expr> items, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.elem = items;
        }
        public override bool Equals(Expr other)
        {
            if (!(other is DictExpr d)) { return false; }
            if (elem.Count != d.elem.Count) { return false; }
            foreach (KeyValuePair<Expr, Expr> kvp in elem)
            {
                if (!d.elem.TryGetValue(kvp.Key, out Expr otherVal)) { return false; }
                if (!kvp.Value.Equals(otherVal)) { return false; }
            }
            return true;
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            foreach (KeyValuePair<Expr, Expr> kvp in elements)
            {
                int pairHash = 17;
                pairHash = (pairHash * 31) + kvp.Key.GetHashCode();
                pairHash = (pairHash * 31) + kvp.Value.GetHashCode();
                hash ^= pairHash;
            }
            return hash;
        }
    }

    public class VarExpr : Expr
    {
        public Token name { get; }
        public bool isPlaceholder { get { return name.type == TokenType.Underscore; } }

        public VarExpr(Token name) : base(name.lineStart, name.lineEnd)
        {
            this.name = name;
        }
        public override bool Equals(Expr other)
        {
            if (!(other is VarExpr v)) { return false; }
            return name.source == v.name.source && isPlaceholder == v.isPlaceholder;
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + name.source.GetHashCode();
            hash = (hash * 31) + isPlaceholder.GetHashCode();
            return hash;
        }
    }

    public class AssignExpr : Expr
    {
        public Expr name { get; } //varExpr or indexExpr or getExpr
        public Expr newValue { get; }

        public AssignExpr(Expr name, Expr newValue) : base(name.lineStart, newValue.lineEnd)
        {
            this.name = name;
            this.newValue = newValue;
        }
        public override bool Equals(Expr other)
        {
            if (!(other is AssignExpr a)) { return false; }
            return name.Equals(a.name) && newValue.Equals(a.newValue);
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + name.GetHashCode();
            hash = (hash * 31) + newValue.GetHashCode();
            return hash;
        }
    }

    public class IncrExpr : Expr
    {
        public Expr name { get; }
        public Token incrType { get; }
        public bool isPost { get; }

        public IncrExpr(Expr name, Token incrType, bool isPost) : base(isPost ? name.lineStart : incrType.lineStart, isPost ? incrType.lineEnd : name.lineEnd)
        {
            this.name = name;
            this.incrType = incrType;
            this.isPost = isPost;
        }
        public override bool Equals(Expr other)
        {
            if (!(other is IncrExpr i)) { return false; }
            return name.Equals(i.name) && incrType.type == i.incrType.type && isPost == i.isPost;
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + name.GetHashCode();
            hash = (hash * 31) + incrType.type.GetHashCode();
            hash = (hash * 31) + isPost.GetHashCode();
            return hash;
        }
    }

    public class BlockExpr : Expr
    {
        public Stmt[] statements { get; }
        public Expr last { get; }
        public BlockExpr(Stmt[] statements, Expr last) : base(statements[0].lineStart, last.lineEnd)
        {
            this.statements = statements;
            this.last = last;
        }
        public override bool Equals(Expr other)
        {
            if (!(other is BlockExpr b)) { return false; }
            if (statements.Length != b.statements.Length) { return false; }
            for (int i = 0; i < statements.Length; i++) { if (!statements[i].Equals(b.statements[i])) { return false; } }
            return last.Equals(b.last);
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            foreach (Stmt s in statements) { hash = (hash * 31) + s.GetHashCode(); }
            hash = (hash * 31) + last.GetHashCode();
            return hash;
        }
    }

    public class CallExpr : Expr
    {
        public Expr caller { get; }
        public Expr[] arguments { get; }

        public CallExpr(Expr caller, Expr[] arguments, int lineEnd) : base(caller.lineStart, lineEnd)
        {
            this.caller = caller;
            this.arguments = arguments;
        }
        public override bool Equals(Expr other)
        {
            if (!(other is CallExpr c)) { return false; }
            if (arguments.Length != c.arguments.Length) { return false; }
            for (int i = 0; i < arguments.Length; i++) { if (!arguments[i].Equals(c.arguments[i])) { return false; } }
            return caller.Equals(c.caller);
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + caller.GetHashCode();
            foreach (Expr e in arguments) { hash = (hash * 31) + e.GetHashCode(); }
            return hash;
        }
    }

    public class FuncExpr : Expr
    {
        public Token[] param { get; }
        public Stmt[] body { get; }

        public FuncExpr(Token[] param, Stmt[] body, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.param = param;
            this.body = body;
        }
        public override bool Equals(Expr other)
        {
            if (!(other is FuncExpr f)) { return false; }
            if (param.Length != f.param.Length || body.Length != f.body.Length) { return false; }
            for (int i = 0; i < param.Length; i++) { if (param[i].source != f.param[i].source) { return false; } }
            for (int i = 0; i < body.Length; i++) { if (!body[i].Equals(f.body[i])) { return false; } }
            return true;
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            foreach (Token t in param) { hash = (hash * 31) + t.source.GetHashCode(); }
            foreach (Stmt s in body) { hash = (hash * 31) + s.GetHashCode(); }
            return hash;
        }
    }

    public class IndexExpr : Expr
    {
        public Expr obj { get; }
        public Expr index { get; }
        public IndexExpr(Expr obj, Expr index, int lineEnd) : base(obj.lineStart, lineEnd)
        {
            this.obj = obj;
            this.index = index;
        }
        public override bool Equals(Expr other)
        {
            if (!(other is IndexExpr i)) { return false; }
            return obj.Equals(i.obj) && index.Equals(i.index);
        }
        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 31) + obj.GetHashCode();
            hash = (hash * 31) + index.GetHashCode();
            return hash;
        }
    }
}
