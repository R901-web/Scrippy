using System;
using System.Collections.Generic;

namespace Scrippy
{
    public interface IExpr { }

    /* EXPRESSION TYPES
     * BinaryExpr
     * GroupingExpr
     * UnaryExpr
     * LiteralExpr -> null, number, string, boolean -> can be formed from 1 token + cannot lead to other IExprs -> must be leaves
     * TernaryExpr
     * ArrayExpr
     * DictionaryExpr
     */


    public class BinaryExpr : IExpr
    {
        public IExpr left { get; }
        public Token op { get; }
        public IExpr right { get; }
        public BinaryExpr(IExpr left, Token op, IExpr right)
        {
            this.left = left;
            this.op = op;
            this.right = right;
        }
    }

    public class GroupingExpr : IExpr
    {
        public IExpr expr { get; }
        public GroupingExpr(IExpr expr)
        {
            this.expr = expr;
        }
    }

    public class LiteralExpr : IExpr
    {
        public object value { get; }
        public LiteralExpr(object value)
        {
            this.value = value;
        }
    }

    public class UnaryExpr : IExpr
    {
        public Token op { get; }
        public IExpr right { get; }
        public UnaryExpr(Token op, IExpr right)
        {
            this.op = op;
            this.right = right;
        }
    }

    public class TernaryExpr : IExpr
    {
        public IExpr left { get; }
        public Token mainOp { get; }
        public IExpr mid { get; }
        public Token sideOp { get; }
        public IExpr right { get; }
        public TernaryExpr(IExpr left, Token mainOp, IExpr mid, Token sideOp, IExpr right)
        {
            this.left = left;
            this.mainOp = mainOp;
            this.mid = mid;
            this.sideOp = sideOp;
            this.right = right;
        }
    }

    public class ArrayExpr : IExpr
    {
        private List<IExpr> elem;
        public IReadOnlyList<IExpr> elements { get { return elem; } }

        public ArrayExpr(List<IExpr> items)
        {
            this.elem = items;
        }
    }

    public class DictExpr : IExpr
    {
        private Dictionary<IExpr, IExpr> elem;
        public IReadOnlyDictionary<IExpr, IExpr> elements { get { return elem; } }

        public DictExpr(Dictionary<IExpr, IExpr> items)
        {
            this.elem = items;
        }
    }
}
