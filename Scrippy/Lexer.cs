using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Scrippy
{
    public class Lexer 
    {
        public string source { get; }

        private List<Token> tk;

        private int current = 0; //current position
        private int start = 0; //start of token

        private int line = 1;
        private int startLine = 1;

        public Lexer(string source)
        {
            this.source = source.Replace("\r\n", "\n"); //replace newlines from \r\n to \n
            this.tk = new List<Token>();
        }

        #region Lex Tokens
        public Token[] lexChars()
        {
            while (!isEnd())
            {
                start = current;
                startLine = line;
                Token? token = tokenize();
                if (token.HasValue) { tk.Add(token.Value); } //crashed at current = 156587360, line = 9586982, tk.Count = 33554432
            }

            tk.Add(token(TokenType.EOF));
            return tk.ToArray();
        }

        private Token? tokenize()
        {
            //get and consume character
            char c = advance();

            switch (c)
            {
                //1 character tokens
                case ';': return token(TokenType.Semicolon);
                case ',': return token(TokenType.Comma);
                case '{': return token(TokenType.LBrace);
                case '}': return token(TokenType.RBrace);
                case '[': return token(TokenType.LSqBrac);
                case ']': return token(TokenType.RSqBrac);
                case '(': return token(TokenType.LParen);
                case ')': return token(TokenType.RParen);
                case '~': return token(TokenType.Range);
                case '.': return token(TokenType.Access);
                case ' ': return null;
                case '\r': return null;
                case '\t': return null;
                case '\n': line++; return null;

                //2 character tokens
                case '-':
                    if (match('-')) { return token(TokenType.Decrement); }
                    else if (match('=')) { return token(TokenType.MinusAssign); }
                    else { return token(TokenType.Minus); }
                case '+':
                    if (match('+')) { return token(TokenType.Increment); }
                    else if (match('=')) { return token(TokenType.PlusAssign); }
                    else { return token(TokenType.Plus); }
                case '*': return match('=') ? token(TokenType.MultAssign) : token(TokenType.Mult);
                case '/':
                    if (match('=')) { return token(TokenType.DivAssign); }
                    else if (match('/')) { lexSingleComment(); return null; }
                    else if (match('*')) { lexMultiComment(); return null; }
                    else { return token(TokenType.Div); }
                case '^': return match('=') ? token(TokenType.PowAssign) : token(TokenType.Power);
                case '%': return match('=') ? token(TokenType.ModAssign) : token(TokenType.Mod);
                case '!':
                    if (match('=')) { return token(TokenType.NotEQ); }
                    else if (match(':')) { return token(TokenType.NotMatch); }
                    else { return token(TokenType.Not); }
                case '&': 
                    if (match('&')) { return token(TokenType.And); }
                    else if (match('=')) { return token(TokenType.AndAssign); }
                    else { return token(TokenType.PatAnd); }
                case '|': 
                    if (match('|')) { return token(TokenType.Or); }
                    else if (match('=')) { return token(TokenType.OrAssign); }
                    else { return token(TokenType.PatOr); }
                case '<':
                    if (match('=')) { return token(TokenType.LessEQ); }
                    else if (match('>')) { return token(TokenType.Spaceship); }
                    else if (match('<')) { return token(TokenType.In); }
                    else { return token(TokenType.Less); }
                case '>':
                    if (match('=')) { return token(TokenType.MoreEQ); }
                    else if (match('>')) { return token(TokenType.Pipe); }
                    else { return token(TokenType.More); }
                case '=': return match('=') ? token(TokenType.Equal) : token(TokenType.Assign);
                case '?':
                    if (match('?')) { return token(TokenType.NullCoalesce); }
                    else if (match('.')) { return token(TokenType.NullAccess); }
                    else if (match(':')) { return token(TokenType.Elvis); }
                    else if (match('=')) { return token(TokenType.NullAssign); }
                    else { return token(TokenType.TernCond); }
                case ':':
                    if (match(':')) { return token(TokenType.Match); }
                    else if (match('=')) { return token(TokenType.RefEQ); }
                    else { return token(TokenType.Colon); }

                //strings, numbers, identifiers
#warning support string interpolation -> recursively create sublexers?
                case '"':
                    if (peek() == '\'' && peekNext() != '"') { advance(); return lexRawString('"', '\''); } //avoid errors like "'"
                    else { return lexString('"'); }
                case '\'':
                    if (peek() == '"' && peekNext() != '\'') { advance(); return lexRawString('\'', '"'); } //consume the 2nd quote
                    else { return lexString('\''); }
                case '_':
                    if (!validIdentify(peek())) { return token(TokenType.Underscore); }
                    else { return lexIdentifier('_'); }

                default:
                    if (char.IsDigit(c)) { return lexNumber(c); }
                    else if (char.IsLetter(c)) { return lexIdentifier(c); }
                    else
                    {
                        error(line, $"Found unexpected character '{c}'");
                        return null;
                    }
            }
        }

        #endregion

        #region Identifier, Number, String
        /* WARNINGS: 
         * No letters in identifier (e.g. _123_234)
         */
        private Token? lexIdentifier(char c) //advance() in tokenize() consumes 1st character
        {
            Debug.Assert(validIdentify(c));

            StringBuilder sb = new StringBuilder(c.ToString());
            while (validIdentify(peek())) { sb.Append(advance()); }

            string text = sb.ToString();
            TokenType type;
            try { type = Token.keywords[text]; }
            catch (KeyNotFoundException) { type = TokenType.Identifier; }

            if (text == "true") { return token(type, true); }
            else if (text == "false") { return token(type, false); }
            else if (text == "null") { return token(type, null); }

            if (text.All(validNum)) { warning(line, "No letters found in identifier"); } //no letters, all number/underscore

            return token(type);
        }

        /* ERRORS: 
         * Fractional hexadecimal / exponent
         * Multiple decimal points
         * Multiple exponents
         * 
         * WARNINGS: 
         * Empty exponents
         * Empty hexadecimals
         */

        private Token? lexNumber(char c)
        {
            Debug.Assert(validNum(c));

            bool hexadecimal = c == '0' && (match('x') || match('X'));
            if (hexadecimal && !validHexNum(peek())) { warning(line, "Empty hexadecimal number literal (default 0)"); }

            StringBuilder sb = new StringBuilder(c.ToString()); //if is hexadecimal c = 0
            Func<char, bool> isValid = hexadecimal ? validHexNum : (Func<char, bool>) validNum; //takes in char, returns bool

            //finish is supposed to just be recovery + synchronization, NOT more error reporting
            void finish()
            {
                //next is valid, or next is a decimal point NOT access
                while (!isEnd() && (isValid(peek()) || (peek() == '.' && isValid(peekNext()) && peekNext() != '_'))) { advance(); }
            }

            while (isValid(peek())) //before fractional part/exponent part
            {
                //warning if _ is last in number -> also works for isEnd
                if (peek() == '_' && (peekNext() != 'E' && peekNext() != 'e' && !isValid(peekNext()))) { warning(line, "Trailing underscore in number literal"); }
                sb.Append(advance()); 
            }
            //fractional part -> if now is exponent just fall through
            if (peek() == '.' && isValid(peekNext())) //if peekNext() is invalid e.g. decimal -> 123.Equals() -> just fall through
            {
                sb.Append(advance()); //consume the .
                if (hexadecimal)
                {
                    error(line, "Fractional hexadecimal number literal");
                    finish();
                    return null;
                }
                //if after . is not valid -> stop lexing number -> probably a method call e.g. 123.Equals()
                if (peek() == '_') { warning(line, "Trailing underscore in number literal"); } //e.g.123._2938
                while (isValid(peek())) { sb.Append(advance()); }

                //2nd decimal point -> 123.345.Equals() not caught, 123.345._eq() not caught, but 123.345.456 caught -> very sad that 123.456._789 not caught
                if (peek() == '.' && isValid(peekNext()) && peekNext() != '_') 
                {
                    error(line, "Multiple decimal points in number literal");
                    finish();
                    return null;
                }
            }
            //exponents -> hexadecimals treat as any other number -> either return null in 2nd part or caught in 1st part
            if (peek() == 'e' || peek() == 'E') 
            {
                sb.Append(advance()); //consume exponent

                bool hadSign = false; //sign not neccesarily right after E, e.g. 3.8E_+29, 3.8E+_29
                bool hadDigit = false;
                while (isValid(peek()) || (!hadSign && (peek() == '-' || peek() == '+')))
                {
                    if (peek() == '-' || peek() == '+') { hadSign = true; } //next time encounter '+' or '-' -> hadSign = true -> not in while loop
                    if (isValid(peek()) && peek() != '_') { hadDigit = true; }
                    sb.Append(advance());
                }

                if (sb[sb.Length - 1] == '_') { warning(line, "Trailing underscore in number literal"); }

                if (!hadDigit) //e.g. 3.8E, or 3.8E+, or 3.8E+__, or 3.8E__+
                {
                    warning(line, "Empty exponent in number literal (default E0)");
                    sb.Append('0');
                }

                if (peek() == '.' && isValid(peekNext()) && peekNext() != '_') { error(line, "Fractional exponent in number literal"); finish(); return null; }
                else if (peek() == 'E' || peek() == 'e') { advance(); error(line, "Multiple exponents in number literal"); finish(); return null; }
            }

            string strVal = sb.ToString().Replace("_", ""); //remove underscores
            double value;
            try
            {
                if (hexadecimal) { value = ulong.Parse(strVal, NumberStyles.HexNumber); }
                else { value = double.Parse(strVal, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent); }
            }
            catch (FormatException) { error(line, "Invalid number literal"); return null; } //should not catch
            catch (OverflowException) { error(line, "Number literal out of range"); return null; }

            return token(TokenType.NumberLiteral, value);
        }

        /* ERRORS: 
         * Unterminated string literal / Newline in string
         * Invalid escape sequence
         * 
         * WARNINGS:
         * String interpolation not supported yet
         */

        private Token? lexString(char quote)
        {
            Debug.Assert(quote == '\"' || quote == '\'');

            string unicodeEscape(int length)
            {
                string str = "";
                while (!isEnd() && str.Length < length && validHexNum(peek())) { str += advance(); }
                if (str.Length < length)
                {
                    if (isEnd()) { return null; }
                    if (peek() == '\n') { return null; } //dont consume -> still there when finish() called
                    else { return null; } //catches invalid + starter
                }
                return str;
            }

            void finish()
            {
                //synchronise to newline or closing quote, whichever first
                while (!isEnd() && peek() != '\n' && peek() != quote) 
                {
                    if (peek() == '\\' && peekNext() == quote) { advance(); } //is an escape sequence
                    advance();
                }
                match(quote); //consume quote if its there
            }

            StringBuilder sb = new StringBuilder();

            while (!isEnd() && peek() != quote && peek() != '\n')
            {
                if (match('\\')) //begin escape seq, consume \
                {
                    if (isEnd()) break; //if backslash is last char

                    char escape = advance(); //consume char after \ -> point to char after \n
                    switch (escape)
                    {
                        case '\"': sb.Append("\""); break;
                        case '\'': sb.Append("\'"); break;
                        case '\\': sb.Append('\\'); break;
                        case '0': sb.Append("\0"); break;
                        case 'b': sb.Append("\b"); break; //stands for backspace -> \b deletes prev. char
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break; //is 7 spaces
                        case 'v': sb.Append('\v'); break;
                        case 'r': sb.Append('\r'); break;
                        case '{': warning(line, "String interpolation not supported yet"); sb.Append("{"); break; //need to escape { and }, all strings are interpolated
                        case '}': warning(line, "String interpolation not supported yet"); sb.Append("}"); break; //in c# to escape is {{ or }}
                        case 'a': sb.Append("\a"); break;

                        //invalid escape seq. = ignored + warning
                        case 'u': //16 bit unicode -> \u2341
                            string str16 = unicodeEscape(4);
                            if (str16 == null) { error(line, "Invalid escape sequence"); finish(); return null; }
                            int num16 = int.Parse(str16, NumberStyles.HexNumber); //guaranteed to be valid hex
                            sb.Append((char) num16);
                            break;
                        case 'x':
                            string str8 = unicodeEscape(2);
                            if (str8 == null) { error(line, "Invalid escape sequence"); finish(); return null; }
                            int num8 = int.Parse(str8, NumberStyles.HexNumber);
                            sb.Append((char) num8);
                            break;
                        case 'U': //support 32 bit unicode -> \U0001F600
                            string str32 = unicodeEscape(8);
                            if (str32 == null) { error(line, "Invalid escape sequence"); finish(); return null; }
                            int num32 = int.Parse(str32, NumberStyles.HexNumber);
                            try { sb.Append(char.ConvertFromUtf32(num32)); }
                            catch (ArgumentOutOfRangeException) { error(line, "Invalid escape sequence"); finish(); return null; }
                            break;
                        default:
                            error(line, "Invalid escape sequence"); 
                            finish(); 
                            return null;
                    }
                }
                else //consume normal char and add to string builder
                {
                    if (peek() == '{' || peek() == '}') { warning(line, "String interpolation not supported yet"); }
                    sb.Append(advance());
                } 
            }
            if (isEnd() || peek() == '\n') //still on single line, multiline string not supported
            {
                error(line, "Unterminated string literal");
                return null;
            }
            match(quote); //consume closing brace
            return token(TokenType.StringLiteral, sb.ToString());
        }

        /* ERRORS: 
         * Unterminated string literal
         */

        private Token? lexRawString(char outer, char inner) //no escape sequences -> just write \ or ' or " -> have to support multiline
        {
            Debug.Assert((outer == '"' && inner == '\'') || (outer == '\'' && inner == '"'));

            StringBuilder sb = new StringBuilder();

            Func<char, char, bool> isCloseQuote = delegate(char c1, char c2) { return c1 == inner && c2 == outer; };

            while (!isEnd() && !isCloseQuote(peek(), peekNext()))
            {
                if (peek() == '\n') { line++; }
                sb.Append(advance());
            }
            if (isEnd())
            {
                error(startLine, line, "Unterminated string literal");
                return null;
            }
            match(inner); //consume closing quotes
            match(outer);

            return token(TokenType.StringLiteral, sb.ToString());
        }

        #endregion

        #region Comments
        private void lexMultiComment() //allow nesting multiline comment e.g. /* lskdfjk /* lskdf */ sldfklf */
        {
            int depth = 1;
            while (depth > 0 && !isEnd())
            {
                if (match('*') && match('/')) { depth--; }
                else if (match('/') && match('*')) { depth++; }
                else if (match('\n')) { line++; }
                else { advance(); }
            }
            if (isEnd() && depth > 0) { warning(startLine, line, "Unterminated multiline comment"); } //else /* 2398 */ -> * and / are consumed -> is end
        }

        private void lexSingleComment() { while (peek() != '\n' && !isEnd()) { advance(); } }
        #endregion

        #region Tokens, Errors, Warnings
        private Token token(TokenType type, object literal = null)
        {
            string text = source.Substring(start, current - start);
            if (type == TokenType.EOF) { text = ""; }
            return new Token(type, text, literal, startLine);
        }

        //line starts at 1 -> have to subtract 1
        private void error(int line, string message = "") { DiagnosticHandler.add(new Diagnostic(line, Program.lines[line - 1], message, DiagnosticLevel.ERROR)); }

        private void error(int lineStart, int lineEnd, string message = "")
        {
            string[] strings = new string[lineEnd - lineStart + 1];
            for (int i = 0; i < strings.Length; i++) { strings[i] = Program.lines[lineStart + i - 1]; }
            DiagnosticHandler.add(new Diagnostic(lineStart, strings, message, DiagnosticLevel.ERROR));
        }

        private void warning(int line, string message = "") { DiagnosticHandler.add(new Diagnostic(line, Program.lines[line - 1], message, DiagnosticLevel.WARNING)); }
        private void warning(int lineStart, int lineEnd, string message = "")
        {
            string[] strings = new string[lineEnd - lineStart + 1];
            for (int i = 0; i < strings.Length; i++) { strings[i] = Program.lines[lineStart + i - 1]; }
            DiagnosticHandler.add(new Diagnostic(lineStart, strings, message, DiagnosticLevel.WARNING));
        }

        #endregion

        #region Peek, Advance, Match
        private bool isEnd(int index = 0) { return current + index >= source.Length; }
        private char peek() { return isEnd() ? '\0' : source[current]; }
        private char peekNext() { return isEnd(1) ? '\0' : source[current + 1]; }
        private char advance() //consumes and returns character
        {
            current++;
            return isEnd(-1) ? '\0' : source[current - 1];
        }
        private bool match(char c) //only consumes if matches
        {
            if (isEnd()) { return false; } //last char
            else if (source[current] != c) { return false; }
            current++;
            return true;
        }
        #endregion

        #region Validation
        //only for after 0x
        private bool validHexNum(char c) { return char.IsDigit(c) || c == '_' || ('A' <= c && c <= 'F') || ('a' <= c && c <= 'f'); }

        //for all characters
        private bool validNum(char c) { return char.IsDigit(c) || c == '_'; }

        //only for after 1st character
        private bool validIdentify(char c) { return char.IsLetterOrDigit(c) || c == '_'; }

        private bool validEscape(char c)
        {
            return
                c == '\'' || c == '\\' || c == '\"' ||
                c == '0' || c == 'a' ||
                c == 'b' || c == 'r' ||
                c == 'n' || c == 't' || c == 'v' ||
                c == '{' || c == '}' ||
                c == 'u' || c == 'U' || c == 'x';
        }
        #endregion
    }
}