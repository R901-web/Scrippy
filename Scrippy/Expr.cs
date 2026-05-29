using System.Collections.Generic;

namespace Scrippy
{
    public abstract class Expr
    {
        public int lineStart { get; }
        public int lineEnd { get; }

        protected Expr(int lineStart, int lineEnd)
        {
            this.lineStart = lineStart;
            this.lineEnd = lineEnd;
        }
    }

    /* EXPRESSION TYPES
     * BinaryExpr
     * GroupingExpr
     * UnaryExpr
     * LiteralExpr -> null, number, string, boolean -> can be formed from 1 token + cannot lead to other IExprs -> must be leaves
     * TernaryExpr
     * ArrayExpr
     * DictionaryExpr
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
    }

    public class GroupingExpr : Expr
    {
        public Expr expr { get; }
        public GroupingExpr(Expr expr, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.expr = expr;
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
    }

    public class ArrayExpr : Expr
    {
        private List<Expr> elem;
        public IReadOnlyList<Expr> elements { get { return elem; } }

        public ArrayExpr(List<Expr> items, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.elem = items;
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
    }
}
