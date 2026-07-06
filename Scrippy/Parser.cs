using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;


namespace Scrippy
{
    /*
    Comma,
    LSqBrac, RSqBrac,
    ObjType, 
    Rest, 
    Access, NullAccess, 
    Pipe, Underscore, 
     */

    /* GRAMMAR: 
     * program -> statement* EOF
     * statment -> varDecl | funcDecl | simple
     * varDecl -> "var" ID ( "," ID )* ( "=" expression )? ";" 
     *            "var" "[" ( ID | "_" ) ( "," ( ID | "_" ) )* "=" expression ";" 
     *            "var" "[" ID ":" ID ( "," ID ":" ID )* "=" expression ";"
     * funcDecl -> "func" ID "(" ( ID, ( "," ID )* ( "," ID "=" primary )* ( "," "params" ID )? )? ")" blockStmt
     *             "func" ID "(" ( ID "=" primary ( "," ID "=" primary )* ( "," "params" ID)? )? ")" blockStmt
     * 
     * simple -> exprStmt | writeStmt | blockStmt | ifStmt | whileStmt | forStmt | switchStmt | returnStmt
     * exprStmt -> expression ";"
     * writeStmt -> "write" "(" expression ")" ";" 
     * blockStmt -> "{" statement* "}"
     * ifStmt -> "if" "(" expression ")" simple ( "else" simple )?
     * whileStmt -> "while" "(" expression ")" simple
     * forStmt -> "for" "(" ( declaration | exprStmt | ";" ) expression? ";" expression? ")" simple
     * returnStmt -> "return" expression? ";"
     * switchStmt -> "switch" "(" expression ")" "{" ( "case" "(" patternLogic ")" simple )* "}"
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
     * spaceship (L) -> pipe ( "<>" pipe )*
     * pipe (L) -> term ( ">>" term )*
     * term (L) -> factor ( ( "-" | "+" ) factor )*
     * factor (L) -> power ( ( "/" | "*" | "%" ) power )*
     * power (R) -> unary ( "^" power )?
     * unary (R) -> ( "!" | "-" | "+" ) unary | prefix 
     * prefix (R) -> ( "++" | "--" )* postfix
     * postfix (L) -> call ( "++" | "--" )*
     * call (L) -> primary ( "(" ( expression ( "," expression )* )? ")" )*
     * 
     * primary -> NUMBER | STRING | BOOL | "null" | "(" expression ")" | ID | "[" "]" | "[" ":" "]" | "_"
     *            "[" expression ("," expression)* "]" | "[" expression ":" expression ("," expression ":" expression)* "]" 
     *            "func" "(" ( ID ( "," ID )* )? ")" blockStmt | ID "->" expression | "func" "(' ( ID ( "," ID )* )? ")" "->" expression
     */

#warning implement ref keyword + default values/variable num of params -> param order must be: required -> default -> param (only 1) -> what about named args
#warning for params keyword -> change to ~? also for var destructuring e.g. var [a, b, ~ c] = [1,2,3,4,5] and func f(a, b, ~ c) {}
    public class Parser
    {
        public Token[] tokens { get; }

        private List<Stmt> program;

        private int current = 0;
        private int hiddenVar = 0;

#warning move to resolver -> also semantic analysis like is in var destr, incr/decr, if return is in method decl + length of destr, has placeholder in pipe + break/cont not in loop + underscore
#warning for assign/incr/decr -> what about a[0]++? or a.b++? or a.b = 5; -> need store expr instead of token
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
                if (match(TokenType.Func)) { return parseFuncDecl(); }
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
            bool isConst = prev().type == TokenType.Const;

            if (match(TokenType.LSqBrac)) { return parseArrDestr(lineStart, isConst); } //var [x, y] = [5, 5]; or const [x, y] = [5, 5];

            if (!match(TokenType.Identifier)) { throw error(peek(), "Identifier expected in variable declaration"); }
            List<Token> names = new List<Token>() { prev() };
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
            if (!match(TokenType.Identifier, TokenType.Underscore)) { throw error(peek(), "Identifier/discard expected in variable declaration"); }
            names.Add(prev());

            if (match(TokenType.Colon) && names[0].type != TokenType.Underscore) { return parseDictDestr(lineStart, isConst, names[0]); }

            while (match(TokenType.Comma))
            {
                if (!match(TokenType.Identifier, TokenType.Underscore)) { throw error(peek(), "Identifier or discard expected in variable declaration"); }
                names.Add(prev());
            }
            if (!match(TokenType.RSqBrac)) { throw error(peek(), "Missing right square bracket ']' in variable destructuring"); }
            if (!match(TokenType.Assign)) { throw error(peek(), "Missing initializer in variable destructuring"); }

            Expr initializer = parseExpr();
            if (!match(TokenType.Semicolon)) { throw error(peek(), "Missing ';' at end of statement"); }
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
            if (!match(TokenType.Semicolon)) { throw error(peek(), "Missing ';' at end of statement"); }
            return new DictDestrStmt(names, initializer, isConst, lineStart, prev().lineEnd);
        }

        private Stmt parseFuncDecl()
        {
            int lineStart = prev().lineStart;

            if (!match(TokenType.Identifier)) { throw error(peek(), "Identifier expected in function declaration"); }
            Token name = prev();

            if (!match(TokenType.LParen)) { throw error(peek(), "Missing left parentheses '(' in function declaration"); }
            List<Token> parameters = new List<Token>();
            while (!match(TokenType.RParen) && !isEnd())
            {
                if (!match(TokenType.Identifier)) { throw error(peek(), "Identifier expected in function declaration"); }
                parameters.Add(prev());
                if (!match(TokenType.Comma) && !peek(TokenType.RParen)) { throw error(peek(), "Comma expected in function declaration"); }
            }
            if (isEnd()) { throw error(peek(), "Missing right parentheses ')' in function declaration"); }
            if (!match(TokenType.LBrace, TokenType.Lambda)) { throw error(peek(), "Missing left brace '{' or lambda in function declaration"); }
            Token bodyStart = prev();
            if (bodyStart.type == TokenType.LBrace)
            {
                BlockStmt body = (BlockStmt) parseBlock();
                return new FuncDeclStmt(name, parameters.ToArray(), body.statements, lineStart, body.lineEnd);
            }
            else if (bodyStart.type == TokenType.Lambda)
            {
                Expr returnVal = parseExpr();
                Stmt returnStmt = new ReturnStmt(returnVal, returnVal.lineStart, returnVal.lineEnd);
                if (!match(TokenType.Semicolon)) { throw error(peek(), "Missing semicolon at end lambda function declaration"); }
                return new FuncDeclStmt(name, parameters.ToArray(), new Stmt[] { returnStmt }, lineStart, returnStmt.lineEnd);
            }
            throw new NotImplementedException();
        }

        private Stmt parseSimple()
        {
            if (match(TokenType.LBrace)) { return parseBlock(); }
            if (match(TokenType.If)) { return parseIf(); }
            if (match(TokenType.While)) { return parseWhile(); }
            if (match(TokenType.For)) { return parseFor(); }
            if (match(TokenType.Break, TokenType.Continue)) { return parseKey(); }
            if (match(TokenType.Switch)) { return parseSwitch(); }
            if (match(TokenType.Return)) { return parseReturn(); }
            return parseExprStmt();
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
            int id = hiddenVar;
            while (match(TokenType.Case))
            {
                if (!match(TokenType.LParen)) { throw error(peek(), "Missing left parentheses '(' in switch statement"); }
                Expr pattern = parsePatternLogic(new VarExpr(new Token(TokenType.Identifier, $"$switch^var_{id}", lineStart)));
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
            Stmt variableAssign = new VarDeclStmt(new Token[] { new Token(TokenType.Identifier, $"$switch^var_{id}", lineStart) }, variable, false, lineStart, lineEnd);
            if (desugared == null) { desugared = new BlockStmt(new Stmt[1] { variableAssign }, lineStart, lineEnd); }
            else { desugared = new BlockStmt(new Stmt[2] { variableAssign, desugared }, lineStart, lineEnd); }
            return desugared;
        }

        private Stmt parseReturn()
        {
            int lineStart = prev().lineStart;
            Expr value = null;
            if (!peek(TokenType.Semicolon)) { value = parseExpr(); }
            if (!match(TokenType.Semicolon)) { throw error(peek(), "Missing semicolon in return statement"); }
            return new ReturnStmt(value, lineStart, prev().lineEnd);
        }
        #endregion

        #region Expressions
        private Expr parseExpr() { return parseAssign(); }

        private Expr parseAssign()
        {
            TokenType[] assignTypes = new TokenType[]
            {
                TokenType.Assign, TokenType.PlusAssign, TokenType.MinusAssign,
                TokenType.DivAssign, TokenType.MultAssign, TokenType.PowAssign, TokenType.ModAssign,
                TokenType.AndAssign, TokenType.OrAssign, TokenType.FalseAssign
            };

            List<Token> names = new List<Token>();
            Expr first = parseTernary();
            if (!(first is VarExpr v1 && !v1.isPlaceholder)) { return first; }
            names.Add(v1.name);
            int save = current;
            while (match(TokenType.Comma))
            {
                Expr next = parseTernary();
                if (!(next is VarExpr v2 && !v2.isPlaceholder)) { current = save; return first; }
                names.Add(v2.name);
            }
            if (!match(assignTypes)) { current = save; return first; }

            Token assignType = prev();
            Expr value = parseTernary();
            if (names.Count == 1 && assignType.type == TokenType.Assign) { return new AssignExpr(names[0], value); }

            hiddenVar++;
            int id = hiddenVar;
            List<Stmt> desugared = new List<Stmt>();
            Stmt varAssign = new VarDeclStmt(new Token[] { new Token(TokenType.Identifier, $"$assign^var_{id}", first.lineStart) }, value, false, first.lineStart, first.lineEnd);
            desugared.Add(varAssign);

            for (int i = 0; i < names.Count; i++)
            {
                Expr newValue;
                Expr var = new VarExpr(new Token(TokenType.Identifier, $"$assign^var_{id}", names[i].lineStart));
                switch (assignType.type)
                {
                    case TokenType.Assign:
                        newValue = var; break;
                    case TokenType.PlusAssign:
                        newValue = new BinaryExpr(new VarExpr(names[i]), new Token(TokenType.Plus, "+", names[i].lineStart), var); break;
                    case TokenType.MinusAssign:
                        newValue = new BinaryExpr(new VarExpr(names[i]), new Token(TokenType.Minus, "-", names[i].lineStart), var); break;
                    case TokenType.MultAssign:
                        newValue = new BinaryExpr(new VarExpr(names[i]), new Token(TokenType.Mult, "*", names[i].lineStart), var); break;
                    case TokenType.DivAssign:
                        newValue = new BinaryExpr(new VarExpr(names[i]), new Token(TokenType.Div, "/", names[i].lineStart), var); break;
                    case TokenType.ModAssign:
                        newValue = new BinaryExpr(new VarExpr(names[i]), new Token(TokenType.Mod, "%", names[i].lineStart), var); break;
                    case TokenType.PowAssign:
                        newValue = new BinaryExpr(new VarExpr(names[i]), new Token(TokenType.Power, "^", names[i].lineStart), var); break;
                    case TokenType.AndAssign:
                        newValue = new BinaryExpr(new VarExpr(names[i]), new Token(TokenType.And, "&&", names[i].lineStart), var); break;
                    case TokenType.OrAssign:
                        newValue = new BinaryExpr(new VarExpr(names[i]), new Token(TokenType.Or, "||", names[i].lineStart), var); break;
                    case TokenType.FalseAssign:
                        newValue = new BinaryExpr(new VarExpr(names[i]), new Token(TokenType.Elvis, "?:", names[i].lineStart), var); break;
                    default:
                        throw error(assignType, "Invalid assign type");
                }

                Expr assign = new AssignExpr(names[i], newValue);
                desugared.Add(new ExprStmt(assign, assign.lineStart));
            }

            return new BlockExpr(desugared.ToArray(), new VarExpr(new Token(TokenType.Identifier, $"$assign^var_{id}", names[0].lineStart)));
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
            else if (ops.Count == 1) { return new BinaryExpr(exprs[0], ops[0], exprs[1]); } //normal comparison e.g. a < b

            //declare vars
            List<Stmt> varAssigns = new List<Stmt>();
            int firstId = hiddenVar + 1;
            foreach (Expr e in exprs)
            {
                hiddenVar++;
                Stmt variableAssign = new VarDeclStmt(new Token[] { new Token(TokenType.Identifier, $"$eq^var_{hiddenVar}", e.lineStart) }, e, false, e.lineStart, e.lineEnd);
                varAssigns.Add(variableAssign);
            }
            //equate vars
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
                int id = hiddenVar;
                Token op = prev();
                Expr desugared = parsePatternLogic(new VarExpr(new Token(TokenType.Identifier, $"$patt^var_{id}", op.lineEnd)));
                if (op.type == TokenType.NotMatch) { desugared = new UnaryExpr(new Token(TokenType.Not, "!", expr.lineStart), desugared); }

                Stmt variableAssign = new VarDeclStmt(new Token[] { new Token(TokenType.Identifier, $"$patt^var_{id}", op.lineStart) }, expr, false, op.lineStart, op.lineEnd);
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
            else if (ops.Count == 1) { return new BinaryExpr(exprs[0], ops[0], exprs[1]); } //normal comparison e.g. a < b

            //assign vars
            List<Stmt> varAssigns = new List<Stmt>();
            int firstId = hiddenVar + 1;
            foreach (Expr e in exprs)
            {
                hiddenVar++;
                Stmt variableAssign = new VarDeclStmt(new Token[] { new Token(TokenType.Identifier, $"$comp^var_{hiddenVar}", e.lineStart) }, e, false, e.lineStart, e.lineEnd);
                varAssigns.Add(variableAssign);
            }
            //compare vars
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
            Expr expr = parsePipe();
            while (match(TokenType.Spaceship))
            {
                Token op = prev();
                Expr right = parsePipe();
                expr = new BinaryExpr(expr, op, right);
            }
            return expr;
        }

        private Expr parsePipe()
        {
            Expr expr = parseTerm();
            while (match(TokenType.Pipe))
            {
                Token op = prev();
                Expr func = parseTerm();
                expr = new CallExpr(func, new Expr[] { expr }, func.lineEnd); //desugaring -> x >> f = f(x)
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
            if (!(right is VarExpr v && !v.isPlaceholder)) { throw error(prev(), "Invalid increment/decrement target"); }

            if (ops.Count == 1) { return new IncrExpr(v.name, ops[0], false); }

            List<Stmt> desugared = new List<Stmt>();
            for (int i = ops.Count - 1; i >= 0; i--)
            {
                Expr incr = new IncrExpr(v.name, ops[i], false);
                desugared.Add(new ExprStmt(incr, ops[i].lineEnd));
            }
            return new BlockExpr(desugared.ToArray(), v);
        }

        private Expr parsePostfix()
        {
            Expr left = parseCall();
            List<Token> ops = new List<Token>();
            while (match(TokenType.Increment, TokenType.Decrement)) { ops.Add(prev()); }
            if (ops.Count == 0) { return left; }
            if (!(left is VarExpr v && !v.isPlaceholder)) { throw error(prev(), "Invalid increment/decrement target"); }

            if (ops.Count == 1) { return new IncrExpr(v.name, ops[0], true); }

            List<Stmt> desugared = new List<Stmt>();
            hiddenVar++;
            int id = hiddenVar;
            Expr clone = new CallExpr(new VarExpr(new Token(TokenType.Identifier, "clone", ops[0].lineStart)), new Expr[] { left }, ops[0].lineEnd);
            Stmt variableAssign = new VarDeclStmt(new Token[] { new Token(TokenType.Identifier, $"$incr^var_{id}", left.lineStart) }, clone, false, left.lineStart, left.lineEnd);
            desugared.Add(variableAssign);
            for (int i = 0; i < ops.Count; i++)
            {
                Expr incr = new IncrExpr(v.name, ops[i], true);
                desugared.Add(new ExprStmt(incr, ops[i].lineEnd));
            }
            return new BlockExpr(desugared.ToArray(), new VarExpr(new Token(TokenType.Identifier, $"$incr^var_{id}", ops[ops.Count - 1].lineStart)));
        }

        private Expr parseCall()
        {
            Expr expr = parsePrimary();
            while (match(TokenType.LParen))
            {
                List<Expr> args = new List<Expr>();

                while (!match(TokenType.RParen) && !isEnd())
                {
                    args.Add(parseExpr());
                    if (!match(TokenType.Comma) && !peek(TokenType.RParen)) { throw error(peek(), "Comma expected in call expression"); }
                }
                if (isEnd()) { throw error(peek(), "Missing right parentheses ')' in call expression"); }

                expr = new CallExpr(expr, args.ToArray(), prev().lineEnd);
            }
            return expr;
        }

#warning make _ desugar into an anonymous func? e.g. _ + 1 desugars to x -> x + 1
        private Expr parsePrimary()
        {
            //literals
            if (match(TokenType.Null)) { return new LiteralExpr(null, prev().lineStart); }
            if (match(TokenType.NumberLiteral, TokenType.StringLiteral, TokenType.BooleanLiteral)) { return new LiteralExpr(prev().literal, prev().lineStart, prev().lineEnd); }
            if (match(TokenType.Underscore)) { return new VarExpr(prev()); }
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

            //Variables + Lambdas
            if (match(TokenType.Identifier))
            {
                Token id = prev();
                if (match(TokenType.Lambda))
                {
                    Expr returnVal = parseExpr();
                    Stmt returnStmt = new ReturnStmt(returnVal, returnVal.lineStart, returnVal.lineEnd);
                    return new FuncExpr(new Token[1] { id }, new Stmt[1] { returnStmt }, id.lineStart, prev().lineEnd);
                }
                return new VarExpr(id);
            }

            //Grouping
            if (match(TokenType.LParen))
            {
                int lineStart = prev().lineStart;
                Expr expr = parseExpr();
                if (!match(TokenType.RParen)) { throw error(prev(), "Missing right parentheses ')' in grouping expression"); }
                return new GroupingExpr(expr, lineStart, prev().lineEnd); //from left and right paren
            }

            //Arrays + Dicts
            if (match(TokenType.LSqBrac))
            {
                int lineStart = prev().lineStart;
                //empty array: [], empty dictionary: [:]
                if (match(TokenType.RSqBrac)) { return new ArrayExpr(new List<Expr>(), lineStart, prev().lineEnd); }
                if (match(TokenType.Colon)) //consume : then ] -> lazy eval 
                {
                    if (!match(TokenType.RSqBrac)) { throw error(peek(), "Missing right square bracket ']' in empty dictionary"); }
                    return new DictExpr(new Dictionary<Expr, Expr>(), lineStart, prev().lineEnd);
                }
                Expr first = parseExpr();

                if (peek().type == TokenType.Comma || peek().type == TokenType.RSqBrac) { return parseArray(first, lineStart); }
                else if (peek().type == TokenType.Colon) { return parseDict(first, lineStart); }
            }

            //Anonymous Funcs + FuncType
            if (match(TokenType.Func))
            {
                if (!peek(TokenType.LParen)) { return new LiteralExpr(typeof(FuncValue), prev().lineStart); }
                int lineStart = prev().lineStart;
                return parseFunc(lineStart);
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
            List<Expr> elements = new List<Expr>() { first };
            while (match(TokenType.Comma))
            {
                Expr next = parseExpr();
                elements.Add(next);
            }
            if (!match(TokenType.RSqBrac)) { throw error(prev(), "Missing right square bracket ']' in array literal"); }
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
                if (!match(TokenType.Colon)) { throw error(prev(), "Missing ':' in dictionary literal"); }
                Expr val = parseExpr();
                elements.Add(key, val);
            }
            if (!match(TokenType.RSqBrac))
            {
                throw error(prev(), "Missing right square bracket ']' in dictionary literal");
            }
            return new DictExpr(elements, lineStart, prev().lineEnd);
        }

        private Expr parseFunc(int lineStart)
        {
            if (!match(TokenType.LParen)) { throw error(peek(), "Missing left parentheses '(' in anonymous function declaration"); }

            List<Token> parameters = new List<Token>();
            while (!match(TokenType.RParen) && !isEnd())
            {
                if (!match(TokenType.Identifier)) { throw error(peek(), "Identifier expected in anonymous function declaration"); }
                parameters.Add(prev());
                if (!match(TokenType.Comma) && !peek(TokenType.RParen)) { throw error(peek(), "Comma expected in anonymous function declaration"); }
            }
            if (isEnd()) { throw error(peek(), "Missing right parentheses ')' in anonymous function declaration"); }


            if (!match(TokenType.LBrace, TokenType.Lambda)) { throw error(peek(), "Missing left brace '{' or lambda in function declaration"); }
            Token bodyStart = prev();
            if (bodyStart.type == TokenType.LBrace)
            {
                BlockStmt body = (BlockStmt) parseBlock();
                return new FuncExpr(parameters.ToArray(), body.statements, lineStart, prev().lineEnd);
            }
            else if (bodyStart.type == TokenType.Lambda)
            {
                Expr returnVal = parseExpr();
                Stmt returnStmt = new ReturnStmt(returnVal, returnVal.lineStart, returnVal.lineEnd);
                return new FuncExpr(parameters.ToArray(), new Stmt[] { returnStmt }, lineStart, prev().lineEnd);
            }

            throw new NotImplementedException();
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
                return new BinaryExpr(variable, op, right);
            }
            else if (match(TokenType.Underscore)) { return new LiteralExpr(true, prev().lineStart); } // e.g. _
            else if (match(TokenType.Minus, TokenType.Not, TokenType.Plus))
            {
                Token op = prev();
                Expr right = parsePrimary();
                Expr lit = new UnaryExpr(op, right);
                return new BinaryExpr(variable, new Token(TokenType.Equal, "==", op.lineStart), lit);
            }
            else //e.g. 5, [:], "str"
            {
                Expr right = parsePrimary();
                if (right is LiteralExpr l && l.value is Type) //a == num different from a :: num
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
