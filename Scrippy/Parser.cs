using System;
using System.Collections.Generic;
using System.Linq;
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

        public IExpr parseTokens()
        {
            try { return parseExpr(); }
            catch (Diagnostic d) when (d.severity == DiagnosticLevel.ERROR) //only catch errors
            { 
                DiagnosticHandler.add(d);
                return null;
            }
        }

        #region Parse
        private IExpr parseExpr() { return parseTernary(); }

        private IExpr parseTernary()
        {
            IExpr expr = parseFallback();
            if (match(TokenType.TernCond))
            {
                Token mainOp = prev();
                IExpr mid = parseTernary();
                if (!match(TokenType.Colon))
                {
                    throw error(prev(), "Missing ':' in ternary expression");
                }
                Token sideOp = prev();
                IExpr right = parseTernary();
                expr = new TernaryExpr(expr, mainOp, mid, sideOp, right);
            }
            return expr;
        }

        private IExpr parseFallback()
        {
            IExpr expr = parseLogical();
            if (match(TokenType.NullCoalesce, TokenType.Elvis))
            {
                Token op = prev();
                IExpr right = parseFallback();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;
        }

#warning since and and or are same precedence -> add warning if no parentheses around them?
        private IExpr parseLogical()
        {
            IExpr expr = parseEqual();
            while (match(TokenType.And, TokenType.Or))
            {
                Token op = prev();
                IExpr right = parseEqual();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;
        }

        private IExpr parseEqual()
        {
            IExpr expr = parseCompare();
            while (match(TokenType.NotEQ, TokenType.Equal, TokenType.RefEQ))
            {
                Token op = prev();
                IExpr right = parseCompare();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;            
        }

        private IExpr parseCompare()
        {
            IExpr expr = parseTerm();
            while(match(TokenType.More, TokenType.MoreEQ, TokenType.Less, TokenType.LessEQ, TokenType.Spaceship))
            {
                Token op = prev();
                IExpr right = parseTerm();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;
        }

        private IExpr parseTerm()
        {
            IExpr expr = parseFactor();
            while (match(TokenType.Plus, TokenType.Minus))
            {
                Token op = prev();
                IExpr right = parseFactor();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;
        }

        private IExpr parseFactor()
        {
            IExpr expr = parsePower();
            while (match(TokenType.Mult, TokenType.Div, TokenType.Mod))
            {
                Token op = prev();
                IExpr right = parsePower();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;
        }
        
        private IExpr parsePower()
        {
            IExpr expr = parseUnary();
            if (match(TokenType.Power))
            {
                Token op = prev();
                IExpr right = parsePower();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;
        }

        private IExpr parseUnary()
        {
            if (match(TokenType.Not, TokenType.Minus, TokenType.Plus))
            {
                Token op = prev();
                IExpr right = parseUnary();
                return new UnaryExpr(op, right);
            }
            return parsePrimary();
        }

        private IExpr parsePrimary()
        {
            if (match(TokenType.Null)) { return new LiteralExpr(null); }

            if (match(TokenType.NumberLiteral, TokenType.StringLiteral, TokenType.BooleanLiteral))
            {
                return new LiteralExpr(prev().literal);
            }

            if (match(TokenType.LParen))
            {
                IExpr expr = parseExpr();
                if (!match(TokenType.RParen))
                {
                    throw error(prev(), "Missing right parentheses ')' in grouping expression");
                }
                return new GroupingExpr(expr);
            }

            if (match(TokenType.LSqBrac))
            {
                if (match(TokenType.RSqBrac)) { return new LiteralExpr(new object[0]); } //empty array literal
                IExpr first = parseExpr();
                if (peek().type == TokenType.Comma || peek().type == TokenType.RSqBrac) { return parseArray(first); }
                else if (peek().type == TokenType.Colon) { return parseDict(first); }
            }

            //if no check -> in empty file -> only EOF -> parser see -> throw error
            if (!isEnd()) { throw error(peek(), $"Found unexpected token '{peek().source}'"); } 
            return null;
        }

        private IExpr parseArray(IExpr first)
        {
            List<IExpr> elements = new List<IExpr>();
            elements.Add(first);
            while (match(TokenType.Comma))
            {
                IExpr next = parseExpr();
                elements.Add(next);
            }
            if (!match(TokenType.RSqBrac))
            {
                throw error(prev(), "Missing right square bracket ']' in array literal");
            }
            return new ArrayExpr(elements);
        }

        private IExpr parseDict(IExpr first) //first is just a key
        {
            Dictionary<IExpr, IExpr> elements = new Dictionary<IExpr, IExpr>();
            match(TokenType.Colon);
            IExpr value = parseExpr();
            elements.Add(first, value);
            while (match(TokenType.Comma))
            {
                IExpr key = parseExpr();
                if (!match(TokenType.Colon))
                {
                    throw error(prev(), "Missing ':' in dictionary literal");
                }
                IExpr val = parseExpr();
                elements.Add(key, val);
            }
            if (!match(TokenType.RSqBrac))
            {
                throw error(prev(), "Missing right square bracket ']' in dictionary literal");
            }
            return new DictExpr(elements);
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
        private Diagnostic error(Token t, string message) { return new Diagnostic(t.line, Program.lines[t.line - 1], message, DiagnosticLevel.ERROR); }
        private Diagnostic warning(Token t, string message) { return new Diagnostic(t.line, Program.lines[t.line - 1], message, DiagnosticLevel.WARNING); }
        #endregion
    }
}
