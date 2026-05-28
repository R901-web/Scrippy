using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Scrippy
{
    public enum TokenType
    {
        //Essential
        Var, Func, //both for declarations and as type
        Const,
        Semicolon, Comma,
        LBrace, RBrace,
        Read, Write,

        //Literals + Data types
        Identifier,
        StringLiteral, NumberLiteral,
        BooleanLiteral, Null,
        LSqBrac, RSqBrac, //for arrays

        //Type names 
        NumType, StrType, BoolType, //if (type(x) == num) or if (type(y) == str) or if (bool(y))
        ArrType, DictType,
        ObjType, 

        //Loops + Conditionals
        If, Else, Elif,
        Switch, Case, Default,
        For, While,
        Break, Continue, Return, //brk, cont, ret

        //Arithmetic
        Minus, Plus,
        Mult, Div,
        Power, Mod,
        Increment, Decrement, //x++ and x--
        LParen, RParen,

        //Logical + Comparison
        Not, And, Or,
        Less, More,
        LessEQ, MoreEQ,
        Equal, NotEQ,
        RefEQ, //:= always treturns true for primitives e.g. 5 := 5
        Spaceship, // <> operator -> returns -1 if left is less, 0 if equal, 1 if left is more
        PatAnd, PatOr, //for pattern matching -> | and &
        Match, NotMatch, //:: and !: e.g. if (x :: < 5 | > 8) {}

        //Other
        Range, In, //~, << e.g. if (x << [1, 2, 3]) {}, for (x << 2 ~ 5) {}
        Elvis, NullCoalesce, //?: and ??
        Access, NullAccess, //. and ?.
        TernCond, Colon, //? in ternary, : for step or else
        Pipe, //f(g(h(x))) = x >> h >> g >> f
        Underscore, //for f(g(2, h(x, 2))  =  x >> h(_, 2) >> g(2, _) >> f, or var [x, _, y] = f(2, 5)

        //Assignment
        Assign, //x = 5
        PlusAssign, MinusAssign, //+= and -=
        MultAssign, DivAssign,
        PowAssign, ModAssign, //^= and %=
        AndAssign, OrAssign, // &= and |=, equivalent to x = x && y, or x = x || y
        NullAssign, // x ?= y, equivalent to x = x ?? y or x = (x != null) ? x : y

        EOF
    }

    public struct Token
    {
        public static readonly Dictionary<string, TokenType> keywords = new Dictionary<string, TokenType>()
        {
            ["var"] = TokenType.Var,
            ["func"] = TokenType.Func, //responsible for both if (x :: func), and func f(x, y) {}
            ["read"] = TokenType.Read,
            ["write"] = TokenType.Write,
            ["const"] = TokenType.Const,
            ["null"] = TokenType.Null,
            ["num"] = TokenType.NumType,
            ["str"] = TokenType.StrType,
            ["bool"] = TokenType.BoolType,
            ["arr"] = TokenType.ArrType,
            ["dict"] = TokenType.DictType,
            ["obj"] = TokenType.ObjType,
            ["if"] = TokenType.If,
            ["elif"] = TokenType.Elif,
            ["else"] = TokenType.Else,
            ["switch"] = TokenType.Switch,
            ["case"] = TokenType.Case,
            ["default"] = TokenType.Default,
            ["for"] = TokenType.For,
            ["while"] = TokenType.While,
            ["break"] = TokenType.Break,
            ["continue"] = TokenType.Continue,
            ["return"] = TokenType.Return,
            ["true"] = TokenType.BooleanLiteral,
            ["false"] = TokenType.BooleanLiteral
        };

        public TokenType type { get; }
        public string source { get; } //actual text of the token (e.g. "var", "+", "x")
        public object literal { get; } //value of token (e.g. 4, true, "bob") -> null for tokens without value (e.g. var, func, +)
        public int line { get; }

        //for tokens with literal value
        public Token(TokenType type, string source, object literal, int line)
        {
            this.type = type;
            this.source = source;
            this.literal = literal;
            this.line = line;
        }

        //for tokens without literal value
        public Token(TokenType type, string source, int line) : this(type, source, null, line) { }
    }
}
