using System;
using System.Collections.Generic;
using System.Linq;

namespace Scrippy
{
    public enum DiagnosticLevel { WARNING, ERROR } //Warning = 0, Error = 1

    public class Diagnostic : Exception, IComparable<Diagnostic>, IEquatable<Diagnostic>
    {
        public int lineStart { get; }
        public string[] source { get; }
        public DiagnosticLevel severity { get; }

        private Diagnostic(int line, string message, DiagnosticLevel severity) : base(message)
        {
            this.lineStart = line;
            this.severity = severity;
        }

        public Diagnostic(int lineStart, string source, string message, DiagnosticLevel severity) : this(lineStart, message, severity)
        {
            this.source = source.Split('\n');
        }

        public Diagnostic(int lineStart, string[] source, string message, DiagnosticLevel severity) : this(lineStart, message, severity)
        {
            this.source = source;
        }

        public override string ToString()
        {
            string result;
            if (source.Length == 1) { result = $"[{severity}] Line {lineStart}: {Message}\n"; }
            else { result = $"[{severity}] Lines {lineStart} - {lineStart + source.Length - 1}: {Message}\n"; }
            for (int i = 0; i < source.Length; i++)
            {
                int numSpaces = 4 - (lineStart + i).ToString().Length; //calculate spaces needed for line numbers to align
                result += new string(' ', numSpaces);
                result += $"{lineStart + i} | {source[i]}" + (i < source.Length - 1 ? "\n" : "");
            }
            return result;
        }

        public int CompareTo(Diagnostic other)
        {
            //sort by severity then line number
            int compareSeverity = other.severity - this.severity; //higher severity first
            if (compareSeverity != 0) { return compareSeverity; }
            int compareStart = this.lineStart - other.lineStart; //lower line number first
            if (compareStart != 0) { return compareStart; }
            return (this.lineStart + this.source.Length) - (other.lineStart + other.source.Length); //lower line number first
        }

        public bool Equals(Diagnostic other)
        {
            bool sourceEqual = this.source.SequenceEqual(other.source);
            bool lineEqual = this.lineStart == other.lineStart;
            bool messageEqual = this.Message == other.Message;
            bool severityEqual = this.severity == other.severity;
            return sourceEqual && lineEqual && messageEqual && severityEqual;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Diagnostic)) { return false; }
            Diagnostic d = (Diagnostic) obj;
            return Equals(d);
        }

        public override int GetHashCode() //by chagtpt
        {
            int hash = 17; //seed number, should be prime -> reduce collisions

            hash = hash * 23 + lineStart.GetHashCode(); //23 is also prime -> reduce collisions
            hash = hash * 23 + Message.GetHashCode();
            hash = hash * 23 + severity.GetHashCode();

            foreach (string s in source)
            {
                hash = hash * 23 + (s?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }


    public static class DiagnosticHandler
    {
        private static List<Diagnostic> diag;

        public static Diagnostic[] diagnostics { get { cleanup(); return diag.ToArray(); } }

        public static Diagnostic[] warnings { get { cleanup(); return diag.Where(x => x.severity == DiagnosticLevel.WARNING).ToArray(); } }
        public static Diagnostic[] errors { get { cleanup(); return diag.Where(x => x.severity == DiagnosticLevel.ERROR).ToArray(); } }

        public static bool hadError { get { return diag.Any(x => x.severity == DiagnosticLevel.ERROR); } }
        public static bool hadWarning { get { return diag.Any(x => x.severity == DiagnosticLevel.WARNING); } }

        static DiagnosticHandler() { diag = new List<Diagnostic>(); }

        public static void add(Diagnostic d) { diag.Add(d); }

        public static void clear() { diag.Clear(); }

        private static void cleanup()
        {
            diag = diag.Distinct().ToList();
            diag.Sort();
        }

        public static void reportAll()
        {
            cleanup();
            Console.WriteLine("Diagnostics: ");
            foreach (Diagnostic d in diag) 
            {
                Console.WriteLine(d);
            }
        }
    }
}
