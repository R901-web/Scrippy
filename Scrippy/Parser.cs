using System;
using System.Collections.Generic;


namespace Scrippy
{
    /*
    Func, 
    Comma,
    LSqBrac, RSqBrac,
    NumType, StrType, BoolType,
    ArrType, DictType, ObjType, 
    Switch, Case, 
    Return, 
    PatAnd, PatOr, 
    Match, NotMatch, 
    Range, In, 
    Access, NullAccess, 
    Pipe, Underscore, 
     */

    /* GRAMMAR: 
     * program -> statement* EOF
     * statment -> declaration | simple
     * declaration -> "var" ID ( "," ID )* ( "=" expression )? ";" 
     *                "var" "[" ID ( "," ID )* "=" expression ";" 
     *                "var" "[" ID ":" ID ( "," ID ":" ID )* "=" expression ";"
     * simple -> exprStmt | writeStmt | blockStmt | ifStmt | whileStmt | forStmt | switchStmt
     * exprStmt -> expression ";"
     * writeStmt -> "write" "(" expression ")" ";" 
     * blockStmt -> "{" statement* "}"
     * ifStmt -> "if" "(" expression ")" simple ( "else" simple )?
     * whileStmt -> "while" "(" expression ")" simple
     * forStmt -> "for" "(" ( declaration | exprStmt | ";" ) expression? ";" expression? ")" simple
     * 
     * switchStmt -> "switch" "(" expression ")" "{" case* "}"
     * case -> "case" "(" patternLogic ")" simple
     * patternLogic -> pattern ( ( "&" | "|" ) pattern )*
     * pattern -> ( ">" | ">=" | "<" | "<=" )? primary | "(" patternLogic ")"
     * 
     * expression -> ternary
     * assignment (R) -> ID ( "," ID )* ( "=" | "+=" | "-=" | "*=" | "/=" | "%=" | "^=" | "|=" | "&=" | "?=" ) assignment | ternary
     * ternary (R) -> fallback ( "?" ternary ":" ternary )?
     * fallback (R) -> logical ( ( "??" | "?:" ) fallback )?
     * logical (L) -> equality ( ( "&&" | "||" ) equality )*
     * equality (L) -> match ( ( "==" | "!=" | ":=" ) match )*
     * match (L) ->  compare ( ( "::" | "!: ) pattern )*
     * comparison (L) -> spaceship ( ( ">" | ">=" | "<" | "<=" ) spaceship )*
     * spaceship (L) -> term ( "<>" term )*
     * term (L) -> factor ( ( "-" | "+" ) factor )*
     * factor (L) -> power ( ( "/" | "*" | "%" ) power )*
     * power (R) -> unary ( "^" power )?
     * unary (R) -> ( "!" | "-" | "+" ) unary | prefix 
     * prefix (R) -> ( "++" | "--" )* postfix
     * postfix (L) -> primary ( "++" | "--" )*
     * primary -> NUMBER | STRING | BOOL | "null" | "(" expression ")" | ID | "[" "]" | "[" ":" "]"
     *            "[" expression ("," expression)* "]" | "[" expression ":" expression ("," expression ":" expression)* "]" 
     *            "read" "(" ")"
     */

    public class Parser
    {
        public Token[] tokens { get; }

        private List<Stmt> program;

        private int current = 0;
        private int hiddenVar = 0;

#warning move to resolver -> also semantic analysis like is in var destr, incr/decr
        private int loopDepth = 0;

        public Parser(Token[] tokens)
        {
            this.tokens = tokens;
            this.program = new List<Stmt>();
        }

        public Stmt[] parseTokens()
        {
            while (!isEnd()) { program.Add(parseStmt()); }
            return program.ToArray();
        }

        #region Statements
        private Stmt parseStmt()
        {
            try
            {
                if (match(TokenType.Var, TokenType.Const)) { return parseVarDecl(); }
                return parseSimple();
            }
            catch (Diagnostic d) when (d.severity == DiagnosticLevel.ERROR)
            {
                synchronize();
                loopDepth = 0;
                DiagnosticHandler.add(d);
                return null;
            }
        }

        private Stmt parseVarDecl()
        {
            int lineStart = prev().lineStart;
            bool isConst = false;
            if (prev().type == TokenType.Const) { isConst = true; }

            if (match(TokenType.LSqBrac)) { return parseArrDestr(lineStart, isConst); } //var [x, y] = [5, 5]; or const [x, y] = [5, 5];

            if (!match(TokenType.Identifier)) { throw error(peek(), "Identifier expected in variable declaration"); }
            List<Token> names = new List<Token>();
            names.Add(prev());
            while (match(TokenType.Comma))
            {
                if (!match(TokenType.Identifier)) { throw error(peek(), "Identifier expected in variable declaration"); }
                names.Add(prev());
            }
            Expr initializer = null;
            if (isConst && peek().type != TokenType.Assign) { throw error(peek(), "Missing initializer in constant declaration"); } //const must be initialized
            if (match(TokenType.Assign)) { initializer = parseExpr(); }
            if (!match(TokenType.Semicolon)) { throw error(peek(), "Missing ';' at end of statement"); }
            return new VarDeclStmt(names.ToArray(), initializer, isConst, lineStart, prev().lineEnd);
        }

        private Stmt parseArrDestr(int lineStart, bool isConst) //var [a, b, c] = ...
        {
            List<Token> names = new List<Token>();
            if (!match(TokenType.Identifier)) { throw error(peek(), "Identifier expected in variable declaration"); }
            names.Add(prev());

            if (match(TokenType.Colon)) { return parseDictDestr(lineStart, isConst, names[0]); }

            while (match(TokenType.Comma))
            {
                if (!match(TokenType.Identifier)) { throw error(peek(), "Identifier expected in variable declaration"); }
                names.Add(prev());
            }
            if (!match(TokenType.RSqBrac)) { throw error(peek(), "Missing right square bracket ']' in variable destructuring"); }
            if (!match(TokenType.Assign)) { throw error(peek(), "Missing initializer in variable destructuring"); }

            Expr initializer = parseExpr();
            if (!(initializer is ArrayExpr a)) { throw error(prev(), "Unsupported data type for array destructuring"); }
            if (!match(TokenType.Semicolon)) { throw error(peek(), "Missing ';' at end of statement"); }

            if (a.elements.Count != names.Count) { throw error(prev(), "Different lengths for array destructuring"); }
            return new ArrDestrStmt(names.ToArray(), initializer, isConst, lineStart, prev().lineEnd);
        }

        private Stmt parseDictDestr(int lineStart, bool isConst, Token first) //var [a: a1, b: b1, c: c1] = ...
        {
            Dictionary<Token, Expr> names = new Dictionary<Token, Expr>();
            Expr firstExpr = parseExpr();
            names.Add(first, firstExpr);
            while (match(TokenType.Comma))
            {
                if (!match(TokenType.Identifier)) { throw error(peek(), "Identifier expected in variable destructuring"); }
                Token name = prev();
                if (!match(TokenType.Colon)) { throw error(peek(), "Missing colon in variable destructuring"); }
                Expr expr = parseExpr();
                names.Add(name, expr);
            }
            if (!match(TokenType.RSqBrac)) { throw error(peek(), "Missing right square bracket ']' in variable destructuring"); }
            if (!match(TokenType.Assign)) { throw error(peek(), "Missing initializer in variable destructuring"); }

            Expr initializer = parseExpr();
            if (!(initializer is DictExpr d)) { throw error(prev(), "Unsupported data type for dictionary destructuring"); }
            if (!match(TokenType.Semicolon)) { throw error(peek(), "Missing ';' at end of statement"); }
            if (d.elements.Count != names.Count) { throw error(prev(), "Different lengths for array destructuring"); }
            return new DictDestrStmt(names, initializer, isConst, lineStart, prev().lineEnd);
        }

        private Stmt parseSimple()
        {
            if (match(TokenType.Write)) { return parseWrite(); }
            if (match(TokenType.LBrace)) { return parseBlock(); }
            if (match(TokenType.If)) { return parseIf(); }
            if (match(TokenType.While)) { return parseWhile(); }
            if (match(TokenType.For)) { return parseFor(); }
            if (match(TokenType.Break, TokenType.Continue))
            {
                if (loopDepth != 0) { return parseKey(); }
                else { throw error(prev(), "Break and continue only allowed in loops"); }
            }
            if (match(TokenType.Switch)) { return parseSwitch(); }
            return parseExprStmt();
        }

        private Stmt parseWrite()
        {
            int lineStart = prev().lineStart;
            if (!match(TokenType.LParen)) { throw error(peek(), "Missing left parantheses '(' in write statement"); }
            Expr value = parseExpr();
            if (!match(TokenType.RParen)) { throw error(peek(), "Missing right parentheses ')' in write statement"); }
            if (!match(TokenType.Semicolon)) { throw error(peek(), "Missing ';' at end of statement"); }
            return new WriteStmt(value, lineStart, prev().lineEnd);
        }

        private Stmt parseBlock()
        {
            int lineStart = prev().lineStart;
            List<Stmt> statements = new List<Stmt>();
            while (!match(TokenType.RBrace))
            {
                if (isEnd()) { throw error(prev(), "Missing '}' at end of block statement"); }
                statements.Add(parseStmt());
            }
            return new BlockStmt(statements.ToArray(), lineStart, prev().lineEnd);
        }

        private Stmt parseExprStmt()
        {
            Expr value = parseExpr();
            if (!match(TokenType.Semicolon)) { throw error(peek(), "Missing ';' at end of statement"); }
            return new ExprStmt(value, prev().lineEnd); //lineEnd of ;
        }

        private Stmt parseIf()
        {
            int lineStart = prev().lineStart;
            if (!match(TokenType.LParen)) { throw error(peek(), "Missing left parentheses '(' in if statement"); }
            Expr cond = parseExpr();
            if (!match(TokenType.RParen)) { throw error(peek(), "Missing right parentheses ')' in if statement"); }

            Stmt ifBranch = parseSimple();
            Stmt elseBranch = null;
            if (match(TokenType.Else)) { elseBranch = parseSimple(); } //already recurses correclty for else if
            return new IfStmt(cond, ifBranch, elseBranch, lineStart, prev().lineEnd);
        }

        private Stmt parseWhile()
        {
            loopDepth++;
            int lineStart = prev().lineStart;
            if (!match(TokenType.LParen)) { throw error(peek(), "Missing left parentheses '(' in while statement"); }
            Expr cond = parseExpr();
            if (!match(TokenType.RParen)) { throw error(peek(), "Missing right parentheses ')' in while statement"); }
            Stmt body = parseSimple();
            loopDepth--;
            return new WhileStmt(cond, body, lineStart, prev().lineEnd);
        }

        private Stmt parseFor()
        {
            loopDepth++;
            int lineStart = prev().lineStart;

            if (!match(TokenType.LParen)) { throw error(peek(), "Missing left parentheses '(' in for statement"); }

            Stmt initializer;
            if (match(TokenType.Semicolon)) { initializer = null; }
            else if (match(TokenType.Var)) { initializer = parseVarDecl(); }
            else { initializer = parseExprStmt(); }

            Expr condition = null;
            if (!peek(TokenType.Semicolon)) { condition = parseExpr(); }
            if (!match(TokenType.Semicolon)) { throw error(peek(), "Missing semicolon in for statmenet"); }

            Expr change = null;
            if (!peek(TokenType.RParen)) { change = parseExpr(); }
            if (!match(TokenType.RParen)) { throw error(peek(), "Missing right parentheses '(' in for statmenet"); }

            Stmt body = parseSimple();

            if (change != null) { body = new BlockStmt(new Stmt[2] { body, new ExprStmt(change, body.lineStart) }, lineStart, body.lineEnd); }//add the change at end
            condition = condition ?? new LiteralExpr(true, lineStart);

            body = new WhileStmt(condition, body, lineStart, prev().lineEnd);

            if (initializer != null) { body = new BlockStmt(new Stmt[2] { initializer, body }, lineStart, body.lineEnd); } //add initializer at front
            loopDepth--;
            return body;
        }

        private Stmt parseKey()
        {
            Token key = prev();
            if (!match(TokenType.Semicolon)) { throw error(peek(), "Missing semicolon at end of statement"); }
            return new KeyStmt(key, key.lineStart, prev().lineEnd);
        }

        private Stmt parseSwitch()
        {
            int lineStart = prev().lineStart;
            hiddenVar++;

            if (!match(TokenType.LParen)) { throw error(peek(), "Missing left parentheses '(' in switch statement"); }
            Expr variable = parseExpr();
            if (!match(TokenType.RParen)) { throw error(peek(), "Missing right parentheses ')' in switch statement"); }
            if (!match(TokenType.LBrace)) { throw error(peek(), "Missing left brace '{' in switch statement"); }

            List<(Expr, Stmt)> cases = new List<(Expr, Stmt)>();
            while (match(TokenType.Case))
            {
                int caseLine = prev().lineStart;
                if (!match(TokenType.LParen)) { throw error(peek(), "Missing left parentheses '(' in switch statement"); }

                Expr pattern = parsePatternLogic(new VarExpr(new Token(TokenType.Identifier, $"$switch^var_{hiddenVar}", lineStart)));

                if (!match(TokenType.RParen)) { throw error(peek(), "Missing right parentheses ')' in switch statement"); }
                Stmt body = parseSimple();
                cases.Add((pattern, body));
            }
            if (cases.Count == 0) { DiagnosticHandler.add(warning(prev(), "Empty switch statement")); }

            if (!match(TokenType.RBrace)) { throw error(peek(), "Missing right brace '}' in switch statement"); }
            int lineEnd = prev().lineEnd;

            //desugaring time
            Stmt desugared = null;
            for (int i = cases.Count - 1; i >= 0; i--)
            {
                (Expr pattern, Stmt body) = cases[i];
                desugared = new IfStmt(pattern, body, desugared, lineStart, lineEnd);
            }
            //add variable assignment at front
            Stmt variableAssign = new VarDeclStmt(new Token[] { new Token(TokenType.Identifier, $"$switch^var_{hiddenVar}", lineStart) }, variable, false, lineStart, lineEnd);
            if (desugared == null) { desugared = new BlockStmt(new Stmt[1] { variableAssign }, lineStart, lineEnd); }
            else { desugared = new BlockStmt(new Stmt[2] { variableAssign, desugared }, lineStart, lineEnd); }
            return desugared;
        }
        #endregion

        #region Expressions
        private Expr parseExpr() { return parseAssign(); }

        private Expr parseAssign()
        {
            bool hadNonVar = false;
            TokenType[] assignTypes = new TokenType[]
            {
                TokenType.Assign, TokenType.PlusAssign, TokenType.MinusAssign,
                TokenType.DivAssign, TokenType.MultAssign, TokenType.PowAssign, TokenType.ModAssign,
                TokenType.AndAssign, TokenType.OrAssign, TokenType.FalseAssign
            };

            List<Expr> exprs = new List<Expr>();
            Expr first = parseTernary();
            if (!(first is VarExpr)) { hadNonVar = true; }
            exprs.Add(first);
            int save = current;
            while (match(TokenType.Comma))
            {
                Expr next = parseTernary();
                if (!(next is VarExpr)) { hadNonVar = true; }
                exprs.Add(next);
            }

            if (hadNonVar)
            {
                if (match(assignTypes)) { throw error(prev(), "Invalid assignment target"); }
                else { current = save; return first; }
            }
            if (!match(assignTypes)) { current = save; return first; } //after commas no assign part -> roll back to start

            //all vars + had a assign
            Token assignType = prev();
            Expr value = parseAssign();

            hiddenVar++;
            List<Stmt> desugared = new List<Stmt>();
            Stmt varAssign = new VarDeclStmt(new Token[] { new Token(TokenType.Identifier, $"$assign^var_{hiddenVar}", first.lineStart) }, value, false, first.lineStart, first.lineEnd);
            desugared.Add(varAssign);

            for (int i = 0; i < exprs.Count; i++)
            {
                Expr newValue;
                Expr var = new VarExpr(new Token(TokenType.Identifier, $"$assign^var_{hiddenVar}", exprs[i].lineStart));
                switch (assignType.type)
                {
                    case TokenType.Assign:
                        newValue = var; break;
                    case TokenType.PlusAssign:
                        newValue = new BinaryExpr(exprs[i], new Token(TokenType.Plus, "+", exprs[i].lineStart), var); break;
                    case TokenType.MinusAssign:
                        newValue = new BinaryExpr(exprs[i], new Token(TokenType.Minus, "-", exprs[i].lineStart), var); break;
                    case TokenType.MultAssign:
                        newValue = new BinaryExpr(exprs[i], new Token(TokenType.Mult, "*", exprs[i].lineStart), var); break;
                    case TokenType.DivAssign:
                        newValue = new BinaryExpr(exprs[i], new Token(TokenType.Div, "/", exprs[i].lineStart), var); break;
                    case TokenType.ModAssign:
                        newValue = new BinaryExpr(exprs[i], new Token(TokenType.Mod, "%", exprs[i].lineStart), var); break;
                    case TokenType.PowAssign:
                        newValue = new BinaryExpr(exprs[i], new Token(TokenType.Power, "^", exprs[i].lineStart), var); break;
                    case TokenType.AndAssign:
                        newValue = new BinaryExpr(exprs[i], new Token(TokenType.And, "&&", exprs[i].lineStart), var); break;
                    case TokenType.OrAssign:
                        newValue = new BinaryExpr(exprs[i], new Token(TokenType.Or, "||", exprs[i].lineStart), var); break;
                    case TokenType.FalseAssign:
                        newValue = new BinaryExpr(exprs[i], new Token(TokenType.Elvis, "?:", exprs[i].lineStart), var); break;
                    default:
                        throw error(assignType, "Invalid assign type");
                }

                Expr assign = new AssignExpr(((VarExpr) exprs[i]).name, newValue);
                desugared.Add(new ExprStmt(assign, assign.lineStart));
            }

            return new BlockExpr(desugared.ToArray(), new VarExpr(new Token(TokenType.Identifier, $"$assign^var_{hiddenVar}", exprs[0].lineStart)));
        }

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
            bool hadAnd = false;
            bool hadOr = false;
            while (match(TokenType.And, TokenType.Or))
            {
                Token op = prev();
                if (op.type == TokenType.And) { hadAnd = true; }
                else if (op.type == TokenType.Or) { hadOr = true; }
                Expr right = parseEqual();
                expr = new BinaryExpr(expr, op, right);
            }
            if (hadAnd && hadOr) { DiagnosticHandler.add(warning(prev(), "And and or have the same precedence")); }
            return expr;
        }

        private Expr parseEqual()
        {
            Expr expr = parseMatch();
            List<Token> ops = new List<Token>();
            List<Expr> exprs = new List<Expr>() { expr };
            while (match(TokenType.RefEQ, TokenType.Equal, TokenType.NotEQ))
            {
                ops.Add(prev());
                exprs.Add(parseMatch());
            }
            if (ops.Count == 0) { if (exprs.Count != 1) { throw new Exception(); } return expr; }

            List<Stmt> varAssigns = new List<Stmt>();

            int firstId = hiddenVar + 1;
            foreach (Expr e in exprs)
            {
                hiddenVar++;
                Stmt variableAssign = new VarDeclStmt(new Token[] { new Token(TokenType.Identifier, $"$eq^var_{hiddenVar}", e.lineStart) }, e, false, e.lineStart, e.lineEnd);
                varAssigns.Add(variableAssign);
            }

            Expr value = new BinaryExpr(new VarExpr(new Token(TokenType.Identifier, $"$eq^var_{firstId}", exprs[0].lineStart)),
                ops[0], new VarExpr(new Token(TokenType.Identifier, $"$eq^var_{firstId + 1}", exprs[1].lineStart)));

            for (int i = 1; i < ops.Count; i++)
            {
                Expr newVal = new BinaryExpr(new VarExpr(new Token(TokenType.Identifier, $"$eq^var_{firstId + i}", exprs[i].lineStart)),
                    ops[i], new VarExpr(new Token(TokenType.Identifier, $"$eq^var_{firstId + i + 1}", exprs[i + 1].lineStart)));
                value = new BinaryExpr(value, new Token(TokenType.And, "&&", ops[i].lineStart), newVal);
            }

            return new BlockExpr(varAssigns.ToArray(), value);
        }

        private Expr parseMatch()
        {
            Expr expr = parseCompare();
            while (match(TokenType.Match, TokenType.NotMatch))
            {
                hiddenVar++;
                Token op = prev();
                Expr desugared = parsePatternLogic(new VarExpr(new Token(TokenType.Identifier, $"$patt^var_{hiddenVar}", op.lineEnd)));
                if (op.type == TokenType.NotMatch) { desugared = new UnaryExpr(new Token(TokenType.Not, "!", expr.lineStart), expr); }

                Stmt variableAssign = new VarDeclStmt(new Token[] { new Token(TokenType.Identifier, $"$patt^var_{hiddenVar}", op.lineStart) }, expr, false, op.lineStart, op.lineEnd);
                expr = new BlockExpr(new Stmt[1] { variableAssign }, desugared);
            }
            return expr;
        }

        private Expr parseCompare()
        {
            Expr expr = parseSpaceship();
            List<Token> ops = new List<Token>();
            List<Expr> exprs = new List<Expr>() { expr };
            while (match(TokenType.More, TokenType.MoreEQ, TokenType.Less, TokenType.LessEQ))
            {
                ops.Add(prev());
                exprs.Add(parseSpaceship());
            }
            if (ops.Count == 0) { if (exprs.Count != 1) { throw new Exception(); } return expr; }

            List<Stmt> varAssigns = new List<Stmt>();

            int firstId = hiddenVar + 1;
            foreach (Expr e in exprs)
            {
                hiddenVar++;
                Stmt variableAssign = new VarDeclStmt(new Token[] { new Token(TokenType.Identifier, $"$comp^var_{hiddenVar}", e.lineStart) }, e, false, e.lineStart, e.lineEnd);
                varAssigns.Add(variableAssign);
            }

            Expr value = new BinaryExpr(new VarExpr(new Token(TokenType.Identifier, $"$comp^var_{firstId}", exprs[0].lineStart)),
                ops[0], new VarExpr(new Token(TokenType.Identifier, $"$comp^var_{firstId + 1}", exprs[1].lineStart)));

            for (int i = 1; i < ops.Count; i++)
            {
                Expr newVal = new BinaryExpr(new VarExpr(new Token(TokenType.Identifier, $"$comp^var_{firstId + i}", exprs[i].lineStart)),
                    ops[i], new VarExpr(new Token(TokenType.Identifier, $"$comp^var_{firstId + i + 1}", exprs[i + 1].lineStart)));
                value = new BinaryExpr(value, new Token(TokenType.And, "&&", ops[i].lineStart), newVal);
            }

            return new BlockExpr(varAssigns.ToArray(), value);
        }

        private Expr parseSpaceship()
        {
            Expr expr = parseTerm();
            while (match(TokenType.Spaceship))
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
            return parsePrefix();
        }

        private Expr parsePrefix()
        {
            List<Token> ops = new List<Token>();
            while (match(TokenType.Increment, TokenType.Decrement)) { ops.Add(prev()); }
            Expr right = parsePostfix();
            if (ops.Count == 0) { return right; }
            if (!(right is VarExpr v)) { throw error(prev(), "Invalid increment/decrement target"); }

            List<Stmt> desugared = new List<Stmt>();
            for (int i = ops.Count - 1; i >= 0; i--)
            {
                Expr incr = new IncrExpr(v.name, ops[i], false);
                desugared.Add(new ExprStmt(incr, ops[i].lineEnd));
            }
            return new BlockExpr(desugared.ToArray(), v);
        }

#warning for lists -> returns changed bcs references shared -> change from var ... = left -> var ... = clone(left);
        private Expr parsePostfix()
        {
            Expr left = parsePrimary();
            List<Token> ops = new List<Token>();
            while (match(TokenType.Increment, TokenType.Decrement)) { ops.Add(prev()); }
            if (ops.Count == 0) { return left; }
            if (!(left is VarExpr v)) { throw error(prev(), "Invalid increment/decrement target"); }

            List<Stmt> desugared = new List<Stmt>();
            hiddenVar++;
            Stmt variableAssign = new VarDeclStmt(new Token[] { new Token(TokenType.Identifier, $"$incr^var_{hiddenVar}", left.lineStart) }, left, false, left.lineStart, left.lineEnd);
            desugared.Add(variableAssign);
            for (int i = 0; i < ops.Count; i++)
            {
                Expr incr = new IncrExpr(v.name, ops[i], true);
                desugared.Add(new ExprStmt(incr, ops[i].lineEnd));
            }
            return new BlockExpr(desugared.ToArray(), new VarExpr(new Token(TokenType.Identifier, $"$incr^var_{hiddenVar}", ops[ops.Count - 1].lineStart)));
        }

        private Expr parsePrimary()
        {
            if (match(TokenType.Null)) { return new LiteralExpr(null, prev().lineStart); }

            if (match(TokenType.NumberLiteral, TokenType.StringLiteral, TokenType.BooleanLiteral))
            {
                return new LiteralExpr(prev().literal, prev().lineStart, prev().lineEnd);
            }

            if (match(TokenType.Identifier)) { return new VarExpr(prev()); }

            if (match(TokenType.ArrType, TokenType.BoolType, TokenType.StrType, TokenType.NumType, TokenType.DictType, TokenType.TypeType))
            {
                Token t = prev();
                switch (t.type)
                {
                    case TokenType.ArrType: return new LiteralExpr(typeof(ArrValue), t.lineStart);
                    case TokenType.BoolType: return new LiteralExpr(typeof(BoolValue), t.lineStart);
                    case TokenType.StrType: return new LiteralExpr(typeof(StrValue), t.lineStart);
                    case TokenType.NumType: return new LiteralExpr(typeof(NumValue), t.lineStart);
                    case TokenType.DictType: return new LiteralExpr(typeof(DictValue), t.lineStart);
                    case TokenType.TypeType: return new LiteralExpr(typeof(TypeValue), t.lineStart);
                }
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
                if (match(TokenType.RSqBrac)) { return new ArrayExpr(new List<Expr>(), lineStart, prev().lineEnd); }
                if (match(TokenType.Colon)) //consume : then ] -> lazy eval 
                {
                    if (!match(TokenType.RSqBrac)) { throw error(peek(), "Missing right square bracket in empty dictionary"); }
                    return new DictExpr(new Dictionary<Expr, Expr>(), lineStart, prev().lineEnd);
                }
                Expr first = parseExpr();
                if (peek().type == TokenType.Comma || peek().type == TokenType.RSqBrac) { return parseArray(first, lineStart); }
                else if (peek().type == TokenType.Colon) { return parseDict(first, lineStart); }
            }

            if (match(TokenType.Read))
            {
                int lineStart = prev().lineStart;
                if (!match(TokenType.LParen)) { throw error(peek(), "Missing right parantheses '(' in read statement"); }
                if (!match(TokenType.RParen)) { throw error(peek(), "Missing right parentheses ')' in read statement"); }
                return new ReadExpr(lineStart, prev().lineEnd);
            }

            //if no check -> in empty file -> only EOF -> parser see -> throw error
            if (tokens.Length != 1)
            {
                if (peek().type != TokenType.EOF) { throw error(peek(), $"Found unexpected token '{peek().source}'"); }
                throw error(prev(), "Missing token at end of expression");
            }
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

        private Expr parsePatternLogic(Expr variable)
        {
            Expr expr = parsePattern(variable);
            while (match(TokenType.PatAnd, TokenType.PatOr))
            {
                Token op = prev();
                Expr right = parsePattern(variable);
                if (op.type == TokenType.PatAnd) { expr = new BinaryExpr(expr, new Token(TokenType.And, "&&", op.lineStart), right); }
                else if (op.type == TokenType.PatOr) { expr = new BinaryExpr(expr, new Token(TokenType.Or, "||", op.lineStart), right); }
            }
            return expr;
        }

        private Expr parsePattern(Expr variable)
        {
            if (match(TokenType.LParen)) // e.g. (< 5 & > 8)
            {
                int lineStart = prev().lineStart;
                Expr expr = parsePatternLogic(variable);
                if (!match(TokenType.RParen)) { throw error(peek(), "Missing right parenthese ')' in grouping expression"); }
                return new GroupingExpr(expr, lineStart, prev().lineEnd);
            }
            else if (match(TokenType.Less, TokenType.LessEQ, TokenType.More, TokenType.MoreEQ, //e.g. >5, <= 10
                TokenType.Equal, TokenType.NotEQ, TokenType.RefEQ))
            {
                Token op = prev();
                Expr right = parsePrimary();
                if (right is ReadExpr) { throw error(prev(), "Read expressions not allowed as patterns"); }
                return new BinaryExpr(variable, op, right);
            }
            else if (match(TokenType.Underscore)) { return new LiteralExpr(true, prev().lineStart); } // e.g. _
            else //e.g. 5, [:], "str"
            {
                Expr right = parsePrimary();
                if (right is ReadExpr) { throw error(prev(), "Read expressions not allowed as patterns"); }
                else if (right is LiteralExpr l && l.value is Type) //a == num different from a :: num
                {
                    return new BinaryExpr(variable, new Token(TokenType.Match, "::", right.lineStart), right);
                }
                return new BinaryExpr(variable, new Token(TokenType.Equal, "==", right.lineStart), right);
            }
        }
        #endregion

        #region Peek, Advance, Match
        //last element in tokens[] will be EOF -> isEnd if current = tokens.Length - 1
        private bool isEnd(int index = 0) { return current + index >= tokens.Length - 1; }
        private Token peek() { return isEnd() ? tokens[tokens.Length - 1] : tokens[current]; } //returns the last token (EOF) if peek out of bounds
        private bool peek(params TokenType[] types)
        {
            if (isEnd()) { return false; }
            foreach (TokenType t in types)
            {
                if (tokens[current].type == t) { return true; }
            }
            return false;
        }
        private Token peekNext() { return isEnd(1) ? tokens[tokens.Length - 1] : tokens[current + 1]; }
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

        private void synchronize()
        {
            while (!isEnd())
            {
                if (peek().type == TokenType.Semicolon) { advance(); return; }

                TokenType t = peek().type;
                if (t == TokenType.Func || t == TokenType.Var ||
                    t == TokenType.For || t == TokenType.While ||
                    t == TokenType.If || t == TokenType.Switch) { return; }

                else { advance(); }
            }
        }
        #endregion
    }
}
