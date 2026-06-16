using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scrippy
{
    public abstract class Stmt
    {
        public int lineStart { get; }
        public int lineEnd { get; }

        protected Stmt(int lineStart, int lineEnd)
        {
            this.lineStart = lineStart;
            this.lineEnd = lineEnd;
        }
    }

    /* STATEMENT TYPES
     * ExprStmt
     * WriteStmt
     * VarDeclStmt
     * BlockStmt
     * ArrDestrStmt
     * DictDestrStmt
     * IfStmt
     * WhileStmt
     * KeyStmt
     */

    public class ExprStmt : Stmt
    {
        public Expr expr { get; }
        public ExprStmt(Expr expr, int lineEnd) : base(expr.lineStart, lineEnd) //lineEnd from ;
        {
            this.expr = expr;
        }
    }

    public class WriteStmt : Stmt
    {
        public Expr expr { get; }
        public WriteStmt(Expr expr, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.expr = expr;
        }
    }

    public class VarDeclStmt : Stmt
    {
        public Token[] names { get; }
        public Expr initializer { get; }
        public bool initialized
        {
            get {  return initializer != null; }
        }
        public bool isConst { get; }

        public VarDeclStmt(Token[] names, Expr initializer, bool isConst, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.names = names;
            this.initializer = initializer;
            this.isConst = isConst;
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
    }

    public class DictDestrStmt : Stmt
    {
        private Dictionary<Token, Expr> nm;
        public IReadOnlyDictionary<Token, Expr> names { get { return nm; } }
        public Expr initializer { get; }
        public bool isConst { get; }

        public DictDestrStmt(Dictionary<Token, Expr> nm, Expr initializer, bool isConst, int lineStart, int lineEnd) : base (lineStart, lineEnd)
        {
            this.nm = nm;
            this.initializer = initializer;
            this.isConst = isConst;
        }
    }

    public class BlockStmt : Stmt
    {
        public Stmt[] statements { get; }

        public BlockStmt(Stmt[] statements, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.statements = statements;
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
    }

    public class KeyStmt : Stmt
    {
        public Token keyword { get; }
        public KeyStmt(Token keyword, int lineStart, int lineEnd) : base(lineStart, lineEnd)
        {
            this.keyword = keyword;
        }
    }
}
