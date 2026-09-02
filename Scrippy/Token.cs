using System.Collections.Generic;

namespace Scrippy
{
    public enum TokenType
    {
        //Essential
        Var, Func, Const, //both for declarations and as type 
        Semicolon, Comma,
        LBrace, RBrace,

        //Literals
        Identifier,
        StringLiteral, NumberLiteral,
        BooleanLiteral, Null,
        LSqBrac, RSqBrac, //for arrays

        //Type names 
        NumType, StrType, BoolType,
        ArrType, DictType,
        ObjType, TypeType,

        //Loops + Conditionals
        If, Else,
        Switch, Case,
        For, While,
        Break, Continue, Return, //break, cont, return

        //Arithmetic
        Minus, Plus,
        Mult, Div,
        Power, Mod, Trunc,
        Increment, Decrement, //x++ and x--
        LParen, RParen,

        //Logical + Comparison + Equality
        Not, And, Or,
        Less, More,
        LessEQ, MoreEQ,
        Equal, NotEQ,
        RefEQ, //:= always treturns true for primitives e.g. 5 := 5
        Spaceship, // <> operator -> returns -1 if left is less, 0 if equal, 1 if left is more
        PatAnd, PatOr,
        Match, NotMatch,

        //Other
        Rest, In, //~ and |> e.g. if (x |> [1, 2, 3]) {}
        Elvis, NullCoalesce, //?: and ??
        Access, NullAccess, //. and ?.
        TernCond, Colon, //? in ternary, : for step or else
        Pipe, //f(g(h(x))) = x >> h >> g >> f
        Lambda, //x -> x + 1, x -> 2 * x
        Underscore, //for f(g(2, h(x, 2))  =  x >> h(_, 2) >> g(2, _) >> f, or var [x, _, y] = f(2, 5)

        //Assignment
        Assign, //x = 5
        PlusAssign, MinusAssign, //+= and -=
        MultAssign, DivAssign,
        PowAssign, ModAssign, //^= and %=
        AndAssign, OrAssign, // &= and |=, equivalent to x = x && y, or x = x || y
        FalseAssign, // x ?= y, equivalent to x = x ?? y or x = (x != null) ? x : y

        EOF
    }

    public struct Token
    {
        public static readonly Dictionary<string, TokenType> keywords = new Dictionary<string, TokenType>()
        {
            ["var"] = TokenType.Var,
            ["func"] = TokenType.Func, //responsible for both if (x :: func), and func f(x, y) {}
            ["const"] = TokenType.Const,
            ["null"] = TokenType.Null,
            ["num"] = TokenType.NumType,
            ["str"] = TokenType.StrType,
            ["bool"] = TokenType.BoolType,
            ["arr"] = TokenType.ArrType,
            ["dict"] = TokenType.DictType,
            ["obj"] = TokenType.ObjType,
            ["type"] = TokenType.TypeType,
            ["if"] = TokenType.If,
            ["else"] = TokenType.Else,
            ["switch"] = TokenType.Switch,
            ["case"] = TokenType.Case,
            ["for"] = TokenType.For,
            ["while"] = TokenType.While,
            ["break"] = TokenType.Break,
            ["cont"] = TokenType.Continue,
            ["return"] = TokenType.Return,
            ["true"] = TokenType.BooleanLiteral,
            ["false"] = TokenType.BooleanLiteral,
        };

        public TokenType type { get; }
        public string[] lines { get; } //actual text of the token (e.g. "var", "+", "x")
        public string source { get { return string.Join("\n", lines); } }

        public object literal { get; } //value of token (e.g. 4, true, "bob") -> null for tokens without value (e.g. var, func, +)
        public int lineStart { get; }

        public int lineEnd
        {
            get { return lineStart + lines.Length - 1; }
        }

        //for tokens with literal value
        public Token(TokenType type, string source, object literal, int lineStart)
        {
            this.type = type;
            this.lines = source.Split('\n');
            this.literal = literal;
            this.lineStart = lineStart;
        }

        //for tokens without literal value
        public Token(TokenType type, string source, int lineStart) : this(type, source, null, lineStart) { }

        public override string ToString()
        {
            return DebugTools.stringify(this);
        }
    }
}
