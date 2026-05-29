using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http.Headers;


namespace Scrippy
{
    /*
    Var, Func, Const, 
    Semicolon, Comma,
    LBrace, RBrace,
    Read, Write,
    NumType, StrType, BoolType, 
    ArrType, DictType,
    ObjType, 
    If, Else, Elif,
    Switch, Case, Default,
    For, While,
    Break, Continue, Return, 
    Increment, Decrement, 
    PatAnd, PatOr, 
    Match, NotMatch, 
    Range, In, 
    Access, NullAccess, 
    Pipe, Underscore, 
    Assign,
    PlusAssign, MinusAssign,
    MultAssign, DivAssign,
    PowAssign, ModAssign, 
    AndAssign, OrAssign, 
    NullAssign,
     */

    /* GRAMMAR: 
     * expression -> ternary
     * ternary (R) -> fallback ( "?" ternary ":" ternary )?
     * fallback (R) -> equality ( ( "??" | "?:" ) fallback )?
     * logical (L) -> equality ( ( "&&" | "||" ) equality )*
     * equality (L) -> comparison ( ( "==" | "!=" | ":=" ) comparison )*
     * comparison (L) -> term ( ( ">" | ">=" | "<" | "<=" | "<>" ) term )*
     * term (L) -> factor ( ( "-" | "+" ) factor )*
     * factor (L) -> power ( ( "/" | "*" | "%" ) power )*
     * power (R) -> unary ( "^" power )?
     * unary (R) -> ( "!" | "-" | "+" ) unary | primary
     * primary -> NUMBER | STRING | BOOL | "null" | "(" expression ")" |
     *            "[" "]" | "[" expression ("," expression)* "]" | "[" expression ":" expression ("," expression ":" expression)* "]" 
     */

    public class Parser
    {
        public Token[] tokens { get; }
        private int current = 0;

        public Parser(Token[] tokens)
        {
            this.tokens = tokens;
        }

        public Expr parseTokens()
        {
            try { return parseExpr(); }
            catch (Diagnostic d) when (d.severity == DiagnosticLevel.ERROR) //only catch errors + warnings shouldnt be thrown
            { 
                DiagnosticHandler.add(d);
                return null;
            }
        }

        #region Parse
        private Expr parseExpr() { return parseTernary(); }

        private Expr parseTernary()
        {
            Expr expr = parseFallback();
            if (match(TokenType.TernCond))
            {
                Token mainOp = prev();
                Expr mid = parseTernary();
                if (!match(TokenType.Colon))
                {
                    throw error(prev(), "Missing ':' in ternary expression");
                }
                Token sideOp = prev();
                Expr right = parseTernary();
                expr = new TernaryExpr(expr, mainOp, mid, sideOp, right);
            }
            return expr;
        }

        private Expr parseFallback()
        {
            Expr expr = parseLogical();
            if (match(TokenType.NullCoalesce, TokenType.Elvis))
            {
                Token op = prev();
                Expr right = parseFallback();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;
        }

        private Expr parseLogical()
        {
            Expr expr = parseEqual();
            while (match(TokenType.And, TokenType.Or))
            {
                Token op = prev();
                Expr right = parseEqual();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;
        }

        private Expr parseEqual()
        {
            Expr expr = parseCompare();
            while (match(TokenType.NotEQ, TokenType.Equal, TokenType.RefEQ))
            {
                Token op = prev();
                Expr right = parseCompare();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;            
        }

        private Expr parseCompare()
        {
            Expr expr = parseTerm();
            while(match(TokenType.More, TokenType.MoreEQ, TokenType.Less, TokenType.LessEQ, TokenType.Spaceship))
            {
                Token op = prev();
                Expr right = parseTerm();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;
        }

        private Expr parseTerm()
        {
            Expr expr = parseFactor();
            while (match(TokenType.Plus, TokenType.Minus))
            {
                Token op = prev();
                Expr right = parseFactor();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;
        }

        private Expr parseFactor()
        {
            Expr expr = parsePower();
            while (match(TokenType.Mult, TokenType.Div, TokenType.Mod))
            {
                Token op = prev();
                Expr right = parsePower();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;
        }
        
        private Expr parsePower()
        {
            Expr expr = parseUnary();
            if (match(TokenType.Power))
            {
                Token op = prev();
                Expr right = parsePower();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;
        }

        private Expr parseUnary()
        {
            if (match(TokenType.Not, TokenType.Minus, TokenType.Plus))
            {
                Token op = prev();
                Expr right = parseUnary();
                return new UnaryExpr(op, right);
            }
            return parsePrimary();
        }

        private Expr parsePrimary()
        {
            if (match(TokenType.Null)) { return new LiteralExpr(null, prev().lineStart); }

            if (match(TokenType.NumberLiteral, TokenType.StringLiteral, TokenType.BooleanLiteral))
            {
                return new LiteralExpr(prev().literal, prev().lineStart, prev().lineEnd);
            }

            if (match(TokenType.LParen))
            {
                int lineStart = prev().lineStart;
                Expr expr = parseExpr();
                if (!match(TokenType.RParen))
                {
                    throw error(prev(), "Missing right parentheses ')' in grouping expression");
                }
                return new GroupingExpr(expr, lineStart, prev().lineEnd); //from left and right paren
            }

            if (match(TokenType.LSqBrac))
            {
                int lineStart = prev().lineStart;
                //empty array: [], empty dictionary: [:]
                if (match(TokenType.RSqBrac)) { return new LiteralExpr(new List<object>(), lineStart, prev().lineEnd); } 
                if (match(TokenType.Colon) && match(TokenType.RSqBrac)) { return new LiteralExpr(new Dictionary<object, object>(), lineStart, prev().lineEnd); }
                Expr first = parseExpr();
                if (peek().type == TokenType.Comma || peek().type == TokenType.RSqBrac) { return parseArray(first, lineStart); }
                else if (peek().type == TokenType.Colon) { return parseDict(first, lineStart); }
            }

            //if no check -> in empty file -> only EOF -> parser see -> throw error
            if (!isEnd()) { throw error(peek(), $"Found unexpected token '{peek().source}'"); }
#warning if missing sth. e.g. 1 ^ -> return null -> pushed up -> null ref exception
            return null;
        }

        private Expr parseArray(Expr first, int lineStart)
        {
            List<Expr> elements = new List<Expr>();
            elements.Add(first);
            while (match(TokenType.Comma))
            {
                Expr next = parseExpr();
                elements.Add(next);
            }
            if (!match(TokenType.RSqBrac))
            {
                throw error(prev(), "Missing right square bracket ']' in array literal");
            }
            return new ArrayExpr(elements, lineStart, prev().lineEnd); //from [ and ]
        }

        private Expr parseDict(Expr first, int lineStart) //first is just a key
        {
            Dictionary<Expr, Expr> elements = new Dictionary<Expr, Expr>();
            match(TokenType.Colon);
            Expr value = parseExpr();
            elements.Add(first, value);
            while (match(TokenType.Comma))
            {
                Expr key = parseExpr();
                if (!match(TokenType.Colon))
                {
                    throw error(prev(), "Missing ':' in dictionary literal");
                }
                Expr val = parseExpr();
                elements.Add(key, val);
            }
            if (!match(TokenType.RSqBrac))
            {
                throw error(prev(), "Missing right square bracket ']' in dictionary literal");
            }
            return new DictExpr(elements, lineStart, prev().lineEnd);
        }

        #endregion

        #region Peek, Advance, Match
        //last element in tokens[] will be EOF -> isEnd if current = tokens.Length - 1
        private bool isEnd(int index = 0) { return current + index >= tokens.Length - 1; }
        private Token peek() { return isEnd() ? tokens[tokens.Length - 1] : tokens[current]; } //returns the last token (EOF) if peek out of bounds
        private Token prev() { return isEnd(-1) ? tokens[tokens.Length - 1] : tokens[current - 1]; }
        private Token advance()
        {
            current++;
            return isEnd(-1) ? new Token(TokenType.EOF, "", 0) : tokens[current - 1];
        }
        private bool match(params TokenType[] types)
        {
            if (isEnd()) { return false; }
            foreach (TokenType t in types)
            {
                if (tokens[current].type == t) { current++; return true; }
            }
            return false;
        }
        #endregion

        #region Warnings and Errors
        //for missing token -> use prev(), point to one before missing
        //for unexpected token -> use peek(), point to that token itself
        private Diagnostic error(Token t, string message) 
        {
            string[] strings = new string[t.lineEnd - t.lineStart + 1];
            for (int i = 0; i < strings.Length; i++) { strings[i] = Program.lines[t.lineStart + i - 1]; }
            return new Diagnostic(t.lineStart, strings, message, DiagnosticLevel.ERROR);
        }
        private Diagnostic warning(Token t, string message) 
        {
            string[] strings = new string[t.lineEnd - t.lineStart + 1];
            for (int i = 0; i < strings.Length; i++) { strings[i] = Program.lines[t.lineStart + i - 1]; }
            return new Diagnostic(t.lineStart, strings, message, DiagnosticLevel.WARNING);
        }
        #endregion
    }
}
