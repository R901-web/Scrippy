using System;
using System.CodeDom.Compiler;
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
            return $"[Type: {t.type}, Source: {t.source}, Line: {t.line}, Literal: {t.literal}]";
        }

        public static string detailString(IExpr i, int indent = 0)
        {
            string ind = new string(' ', 4 * indent);
            switch (i)
            {
                case BinaryExpr b:
                    return $"{ind}BinaryExpr\n" +
                    $"{ind}{{\n" +
                    $"{detailString(b.left, indent + 1)}\n" +
                    $"{ind}    Op: {b.op.source}\n" +
                    $"{detailString(b.right, indent + 1)}\n" +
                    $"{ind}}}";
                case GroupingExpr g:
                    return $"{ind}GroupingExpr\n" +
                    $"{ind}{{\n" +
                    $"{detailString(g.expr, indent + 1)}\n" +
                    $"{ind}}}";
                case UnaryExpr u:
                    return $"{ind}UnaryExpr\n" +
                    $"{ind}{{\n" +
                    $"{ind}    Op: {u.op.source}\n" +
                    $"{detailString(u.right, indent + 1)}\n" +
                    $"{ind}}}";
                case LiteralExpr l:
                    if (l.value is string) { return $"{ind}LiteralExpr {{ \"{l.value}\" }}"; } //add quotes so I know its string
                    return $"{ind}LiteralExpr {{ {l.value} }}";
                case TernaryExpr t:
                    return $"{ind}TernaryExpr\n" +
                    $"{ind}{{\n" +
                    $"{detailString(t.left, indent + 1)}\n" +
                    $"{ind}    MainOp: {t.mainOp.source}\n" +
                    $"{detailString(t.mid, indent + 1)}\n" +
                    $"{ind}    SideOp: {t.sideOp.source}\n" +
                    $"{detailString(t.right, indent + 1)}\n" +
                    $"{ind}}}";
                    
                case ArrayExpr a:
                    string s1 = $"{ind}ArrayExpr\n" +
                        $"{ind}{{\n";
                    foreach (IExpr e in a.elements) { s1 += detailString(e, indent + 1) + "\n"; }
                    s1 += $"{ind}}}";
                    return s1;
                case DictExpr d:
                    string s2 = $"{ind}DictExpr\n" +
                        $"{ind}{{\n";
                    foreach (KeyValuePair<IExpr, IExpr> kvp in d.elements) 
                    { 
                        s2 += $"{detailString(kvp.Key, indent + 1)}\n";
                        s2 += $"{detailString(kvp.Value, indent + 1)}\n";
                        if (kvp.Key != d.elements.Keys.Last()) { s2 += "\n"; }
                    }
                    s2 += $"{ind}}}";
                    return s2;
                    
            }
            return null;
        }

        public static string easyString(IExpr obj)
        {
            if (obj == null) { return "null"; }

            if (obj is GroupingExpr g) { return $"{easyString(g.expr)}"; }
            if (obj is UnaryExpr u) { return $"({u.op.source}{easyString(u.right)})"; }
            if (obj is BinaryExpr b) { return $"({easyString(b.left)} {b.op.source} {easyString(b.right)})"; }
            if (obj is TernaryExpr t) { return $"({easyString(t.left)} {t.mainOp.source} {easyString(t.mid)} {t.sideOp.source} {easyString(t.right)})"; }
            if (obj is LiteralExpr le) 
            { 
                if (le.value is string) { return $"\"{le.value}\""; } //add quotes so I know its string
                return $"{le.value}"; 
            }
            
            if (obj is ArrayExpr a)
            {
                string s = "[";
                for (int i = 0; i < a.elements.Count; i++)
                {
                    s += easyString(a.elements[i]);
                    s += (i < a.elements.Count - 1) ? ", " : "";
                }
                s += "]";
                return s;
            }
            if (obj is DictExpr d)
            {
                string s = "[";
                for (int i = 0; i < d.elements.Count; i++)
                {
                    s += $"{easyString(d.elements.Keys.ElementAt(i))}: {easyString(d.elements.Values.ElementAt(i))}";
                    s += (i < d.elements.Count - 1) ? ", " : "";
                }
                s += ']';
                return s;
            }
            
            return obj.ToString();
        }
    }
}
#endif
