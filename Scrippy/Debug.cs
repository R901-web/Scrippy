using System.Collections.Generic;
using System.Linq;

#if DEBUG
namespace Scrippy
{
    //this is for me to debug not for users to debug
    public static class DebugTools
    {
        public static string stringify(Token t)
        {
            if (t.lineStart == t.lineEnd) { return $"[Type: {t.type}, Source: {t.source}, Line: {t.lineStart}, Literal: {t.literal}]"; }
            return $"[Type: {t.type}, Source: {t.source}, Lines: {t.lineStart} - {t.lineEnd}, Literal: {t.literal}]";
        }

        public static string stringify(Expr i, int indent = 0)
        {
            string ind = new string(' ', 4 * indent);
            switch (i)
            {
                case BinaryExpr b:
                    return $"{ind}BinaryExpr\n" +
                    $"{ind}{{\n" +
                    $"{stringify(b.left, indent + 1)}\n" +
                    $"{ind}    Op: {b.op.source}\n" +
                    $"{stringify(b.right, indent + 1)}\n" +
                    $"{ind}}}";
                case GroupingExpr g:
                    return $"{ind}GroupingExpr\n" +
                    $"{ind}{{\n" +
                    $"{stringify(g.expr, indent + 1)}\n" +
                    $"{ind}}}";
                case UnaryExpr u:
                    return $"{ind}UnaryExpr\n" +
                    $"{ind}{{\n" +
                    $"{ind}    Op: {u.op.source}\n" +
                    $"{stringify(u.right, indent + 1)}\n" +
                    $"{ind}}}";
                case LiteralExpr l:
                    if (l.value is string) { return $"{ind}LiteralExpr {{ \"{l.value}\" }}"; } //add quotes so I know its string
                    return $"{ind}LiteralExpr {{ {l.value} }}";
                case TernaryExpr t:
                    return $"{ind}TernaryExpr\n" +
                    $"{ind}{{\n" +
                    $"{stringify(t.left, indent + 1)}\n" +
                    $"{ind}    MainOp: {t.mainOp.source}\n" +
                    $"{stringify(t.mid, indent + 1)}\n" +
                    $"{ind}    SideOp: {t.sideOp.source}\n" +
                    $"{stringify(t.right, indent + 1)}\n" +
                    $"{ind}}}";

                case ArrayExpr a:
                    string s1 = $"{ind}ArrayExpr\n" +
                        $"{ind}{{\n";
                    foreach (Expr e in a.elements) { s1 += stringify(e, indent + 1) + "\n"; }
                    s1 += $"{ind}}}";
                    return s1;
                case DictExpr d:
                    string s2 = $"{ind}DictExpr\n" +
                        $"{ind}{{\n";
                    foreach (KeyValuePair<Expr, Expr> kvp in d.elements)
                    {
                        s2 += $"{stringify(kvp.Key, indent + 1)}\n";
                        s2 += $"{stringify(kvp.Value, indent + 1)}\n";
                        if (kvp.Key != d.elements.Keys.Last()) { s2 += "\n"; }
                    }
                    s2 += $"{ind}}}";
                    return s2;
                case VarExpr v:
                    return $"{ind}VarExpr {{ {v.name.source} }}";
                case IncrExpr ie:
                    return $"{ind}IncrExpr\n" +
                    $"{ind}{{\n" +
                    $"{ind}    Name: {ie.name.source}\n" +
                    $"{ind}    Type: {ie.incrType.type}\n" +
                    $"{ind}    Position: {(ie.isPost ? "Postfix" : "Prefix")}\n" +
                    $"{ind}}}";
                case AssignExpr ae:
                    return $"{ind}AssignExpr\n" +
                    $"{ind}{{\n" +
                    $"{ind}    Name: {ae.name.source}\n" +
                    $"{stringify(ae.newValue, indent + 1)}\n" +
                    $"{ind}}}\n";
                case BlockExpr b:
                    string s4 = $"{ind}BlockExpr\n" +
                    $"{ind}{{\n";
                    foreach (Stmt st in b.statements) { s4 += stringify(st, indent + 1) + "\n"; }
                    s4 += stringify(b.last, indent + 1) + "\n";
                    s4 += $"{ind}}}";
                    return s4;
                case CallExpr c:
                    string s5 = $"{ind}CallExpr\n" +
                    $"{ind}{{\n" +
                    $"{stringify(c.caller, indent + 1)}\n\n";
                    foreach (Expr e in c.arguments) { s5 += $"{stringify(e, indent + 1)}\n"; }
                    return s5 + $"{ind}}}";
                case FuncExpr f:
                    string s6 = $"{ind}FuncExpr\n" +
                    $"{ind}{{\n";
                    foreach (Token t in f.param) { s6 += $"{ind}    Param: {t.source}\n"; }
                    s6 += "\n";
                    foreach (Stmt st in f.body) { s6 += stringify(st, indent + 1) + "\n"; }
                    s6 += $"{ind}}}\n";
                    return s6;

            }
            return null;
        }

        public static string stringify(Stmt program, int indent = 0)
        {
            string ind = new string(' ', 4 * indent);
            switch (program)
            {
                case ExprStmt e:
                    return $"{ind}ExprStmt\n" +
                    $"{ind}{{\n" +
                    $"{stringify(e.expr, indent + 1)}\n" +
                    $"{ind}}}\n";
                case VarDeclStmt v:
                    string s = $"{ind}VarDeclStmt\n" +
                    $"{ind}{{\n";
                    s += $"{ind}    IsConst: {v.isConst}\n";
                    foreach (Token t in v.names) { s += $"{ind}    Name: {t.source}\n"; }
                    if (!v.initialized) { return s + $"{ind}}}\n"; }
                    return s +
                    $"\n{stringify(v.initializer, indent + 1)}\n" +
                    $"{ind}}}\n";
                case ArrDestrStmt a:
                    string s2 = $"{ind}ArrDestrStmt\n" +
                    $"{ind}{{\n";
                    s2 += $"{ind}    IsConst: {a.isConst}\n\n";
                    foreach (Token t in a.names) { s2 += $"{ind}    Name: {t.source}\n"; }
                    return s2 +
                    $"\n{stringify(a.initializer, indent + 1)}\n" +
                    $"{ind}}}\n";
                case DictDestrStmt d:
                    string s3 = $"{ind}DictDestrStmt\n" +
                    $"{ind}{{\n";
                    s3 += $"{ind}    IsConst: {d.isConst}\n\n";
                    foreach (KeyValuePair<Token, Expr> kvp in d.names)
                    {
                        s3 += $"{ind}    {stringify(kvp.Key)}\n";
                        s3 += $"{stringify(kvp.Value, indent + 1)}\n";
                        s3 += "\n";
                    }
                    return s3 +
                    $"\n{stringify(d.initializer, indent + 1)}\n" +
                    $"{ind}}}\n";
                case BlockStmt b:
                    string s4 = $"{ind}BlockStmt\n" +
                    $"{ind}{{\n";
                    foreach (Stmt st in b.statements) { s4 += stringify(st, indent + 1) + "\n"; }
                    s4 += $"{ind}}}\n";
                    return s4;
                case IfStmt i:
                    return $"{ind}IfStmt\n" +
                    $"{ind}{{\n" +
                    $"{stringify(i.condition, indent + 1)}\n\n" +
                    $"{stringify(i.ifBranch, indent + 1)}\n" +
                    $"{(i.elseBranch == null ? null : stringify(i.elseBranch, indent + 1))}\n" +
                    $"{ind}}}\n";
                case WhileStmt w:
                    return $"{ind}WhileStmt\n" +
                    $"{ind}{{\n" +
                    $"{stringify(w.condition, indent + 1)}\n\n" +
                    $"{stringify(w.body, indent + 1)}\n" +
                    $"{ind}}}\n";
                case KeyStmt k:
                    return $"{ind}KeyStmt {{ {k.keyword.source} }}\n";
                case FuncDeclStmt f:
                    string s5 = $"{ind}FuncDeclStmt\n" +
                    $"{ind}{{\n" +
                    $"{ind}    Name: {f.name.source}\n\n";
                    foreach (Token t in f.param) { s5 += $"{ind}    Param: {t.source}\n"; }
                    s5 += "\n";
                    foreach (Stmt st in f.body) { s5 += stringify(st, indent + 1) + "\n"; }
                    s5 += $"{ind}}}\n";
                    return s5;
                case ReturnStmt r:
                    return $"{ind}ReturnStmt\n" +
                    $"{ind}{{\n" +
                    $"{(r.value == null ? "\n" : stringify(r.value, indent + 1) + "\n")}" +
                    $"{ind}}}\n";

            }
            return null;
        }
    }
}
#endif
