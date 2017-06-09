using System;
using System.Collections.Generic;
using real = System.Double;

class Parser
{
    string buf;
    string origbuf;
    char token;
    int pos;
    int line;
    string attr;
    Program prog;
    BuiltinOp op;

    //Function topfun = null;
    Function curfun = null;

    List<string> funnames = new List<string>();

    internal Parser(string _b, Program _p)
    {
        buf = (origbuf = _b) + "\0";
        pos = 0;
        prog = _p;
        line = 1;
    }

    class ParseError : Exception { internal Node n; }

    Node Error(string s)
    {
        throw new ParseError
        {
            n = new Node(new Unparsed
            {
                name = origbuf,
                errorpos = pos,
                parseerr = s + " (line: " + line + ")"
            })
        };
    }

    void Op(string name, int p, int flags = 0)
    {
        token = 'O';
        op = prog.uniques.GetOp(name, p, flags);
    }

    void Next()
    {
        op = null;     // token is not an operator

        for(;;) switch(token = buf[pos++])
        {
            case '\0': pos--; token = 'E'; return;
            case '\n': line++; continue;
            case ' ': case '\t': case '\r': continue;

            case '(': case ')': case ',': case '`': case '[': case ']': return;

            case '+': Op("+", 50, BuiltinOp.is_add); return;
            case '-': Op("-", 50, BuiltinOp.unary_and_binary); return;
            case '*': Op("*", 60); return;
            case '/': Op("/", 60); return;

            case '=':
                if (buf[pos] == '=') { pos++; Op("==", 40, BuiltinOp.int_result); }
                else if (buf[pos] == '>') { pos++; Op("=>", 5, BuiltinOp.right_assoc); }
                else Op("=", 20, BuiltinOp.right_assoc);

                return;

            case '>':
                if (buf[pos] == '=') { pos++; Op(">=", 41, BuiltinOp.int_result); }
                else Op(">", 41, BuiltinOp.int_result);
                return;

            case '<':
                if (buf[pos] == '=') { pos++; Op("<=", 41, BuiltinOp.int_result); }
                else Op("<", 41, BuiltinOp.int_result);
                return;

            case '!':
                if (buf[pos] == '=') { pos++; Op("!=", 40, BuiltinOp.int_result); }
                else Op("!", 40, BuiltinOp.unary_only | BuiltinOp.int_to_int);
                return;

            case '|':
                if (buf[pos] == '>') { pos++; Op("|>", 30); }
                else Error("unimplemented operator: |");
                return;

            case ';': Op(";", 10, BuiltinOp.right_assoc); return;

            case '\"':
                attr = "";
                token = 'S';
                for (; ; )
                {
                    char c = buf[pos];
                    if (c < ' ') Error("illegal character in string constant: " + (int)c);
                    pos++;
                    if (c == '\"') break;
                    attr += c;
                }
                return;

            default:
            {
                attr = token.ToString();
                if(Char.IsLetter(token) || token=='_')
                {
                    while(Char.IsLetter(buf[pos]) || buf[pos]=='_') attr += buf[pos++];
                    token = 'I';
                }
                else if(Char.IsDigit(token))
                {
                    while(Char.IsDigit(buf[pos])) attr += buf[pos++];
                    token = 'N';
                    if (buf[pos] == '.')
                    {
                        pos++;
                        attr += '.';
                        while (Char.IsDigit(buf[pos])) attr += buf[pos++];
                        token = 'F';
                    }
                }
                else
                {
                    Error("unknown character: " + (token<' ' ? ((int)token).ToString() : attr));
                }
                return;
            }
        }
    }

    string Token2Str(char t)
    {
        switch(t)
        {
            case 'E': return "end of code";
            case 'I': return "identifier";
            case 'N': return "integer";
            case 'F': return "float";
            case 'S': return "string";
            case 'O': return "operator";
            default: return t.ToString();
        }
    }

    void Expect(char t) { if(token!=t) Error(Token2Str(t)+" expected"); Next(); }

    internal Node Parse(Function parent)
    {
        try
        {
            Next();
            curfun = parent;
            var exp = ParseExp();
            Expect('E');
            curfun = null;
            //if (topfun != null) parent.Add(topfun);
            return exp;
        }
        catch(ParseError pe)
        {
            // shouldn't be needed, but incase typecheck doesn't reach all nodes
            pe.n.err = (pe.n.t as Unparsed).parseerr;
            //topfun = null;
            return pe.n;
        }
    }

    Node ParseExp(int minprec = 0)
    {
        var n = ParseFactor();
        // deal with trailing lower prec of any level, guaranteed to consume whole exp
        while (op != null && op.precedence > minprec) n = ParseExp(n, minprec);
        return n;
    }

    Node ParseExp(Node n, int minprec)
    {
        // Right Assoc -> Recursive
        if ((op.flags & BuiltinOp.right_assoc) != 0)
            return ParseRHS(n, op.precedence-1);

        int thisprec = op.precedence;
        // Left Assoc -> Loop
        while (op!=null && op.precedence == thisprec) n = ParseRHS(n, thisprec);

        // deal with trailing lower prec than this
        if (op != null && op.precedence > minprec && op.precedence < thisprec)
            return ParseExp(n, minprec);

        return n;
    }

    Node ParseRHS(Node lhs, int minprec)
    {
        if ((op.flags & BuiltinOp.unary_only) != 0)
            Error(op.name + " cannot be used as a binary operator");
        var sop = op;
        Next();

        if (sop.name == "=")
        {
            var ph = lhs.t as PlaceHolder;
            if (ph == null) Error("left of = must be a variable");
            return new Node(sop, ParseExp(minprec), lhs); // swap order to keep consistent L->R eval
        }
        else if (sop.name == "=>")
        {
            var f = prog.GenFun(funnames);
            funnames.Add(f.name);

            //if(curfun!=null)
            // FIXME: what if an error occurs below? we'd have a half finished function
            curfun.Add(f);
            //else topfun = f;

            var bf = curfun;
            curfun = f;
            f.root = ParseExp(minprec);
            curfun = bf;

            if(lhs.t is BuiltinOp)
            {
                switch(lhs.t.name)
                {
                    case "=":
                        var ph = lhs.At(1).t as PlaceHolder;
                        f.args.Add(ph);
                        return new Node(f, lhs.At(0));

                    case "__tuple":
                        lhs.ForEach(n =>
                        {
                            if (!(n.t is PlaceHolder))
                                Error("lambda parameters must be identifiers");
                            f.args.Add(n.t as PlaceHolder);
                        });
                        return new Node(new FunctionValue { reff = f, name = f.name });// fixme: dup
                }
            }
            else if (lhs.t is PlaceHolder)
            {
                f.args.Add(lhs.t as PlaceHolder);
                return new Node(new FunctionValue { reff = f, name = f.name });
            }
            Error("left of => must be a variable/tuple or assignment");

        }
        else if (sop.name == "|>")
        {
            return new Node(sop, ParseExp(minprec), lhs);   // same order as __apply
        }

        return new Node(sop, lhs, ParseExp(minprec));
    }

    Node ParseFactor()
    {
        Node r;
        switch(token)
        {
            case 'N':
                r = new Node(prog.uniques.GetInt(int.Parse(attr)));
                Next();
                return r;

            case 'F':
                r = new Node(prog.uniques.GetReal(real.Parse(attr)));
                Next();
                return r;

            case 'S':
                r = new Node(prog.uniques.GetStr(attr));
                Next();
                return r;

            case '(':
                Next();
                r = ParseExp();
                if (token == ',')
                {
                    r = new Node(prog.uniques.tupleop, r);
                    do
                    {
                        Next();
                        r.Add(ParseExp());
                    } while (token == ',');
                }
                Expect(')');
                return r;

            case '[':
                Next();
                r = new Node(prog.uniques.listop);
                if (token != ']') for (; ; )
                {
                    r.Add(ParseExp());
                    if (token == ']') break;
                    Expect(',');
                }
                Next();
                return r;

            case '`':
                Next();
                r = new Node(new FunctionValue { name = attr, undeclared = true });
                Expect('I');
                return r;

            case 'I':
                string name = attr;
                r = new Node(new PlaceHolder { name = name, undeclared = true });
                Next();

                bool isapply;
                if (isapply = token == '!')
                {
                    Next();
                    r = new Node(prog.uniques.applyop, r);
                }

                if (token == '(')
                {
                    Next();

                    if (!isapply)
                    {
                        Builtin bf;
                        prog.uniques.builtins.TryGetValue(name, out bf);

                        r = new Node(bf ?? (NodeType)new Function { name = name, undeclared = true });
                    }

                    if(token!=')') for(;;)
                    {
                        r.Add(ParseExp());
                        if(token==')') break;
                        Expect(',');
                    }
                    Next();
                }

                return r;

            default:
                if (op!=null)
                {
                    if ((op.flags & (BuiltinOp.unary_only | BuiltinOp.unary_and_binary)) == 0)
                        Error(op.name + " cannot be used as a unary operator");

                    BuiltinOp sop = op;
                    Next();
                    // FIXME: set this to whatever minprec the op requires
                    return new Node(sop, ParseExp(100));
                }

                return Error("expression expected");
        }
    }
}
