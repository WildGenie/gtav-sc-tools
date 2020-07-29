﻿namespace ScTools.ScriptLang
{
    using System;
    using System.Collections.Generic;

    using Antlr4.Runtime;

    using ScTools.ScriptLang.Ast;
    using ScTools.ScriptLang.Grammar;

    public static class Test
    {
        const string Code = @"
SCRIPT_NAME test

STRUCT VEC3
    FLOAT X
    FLOAT Y
    FLOAT Z
ENDSTRUCT

STRUCT VEC2
    FLOAT X
    FLOAT Y
ENDSTRUCT


STRUCT RECT_DETAILS
    FLOAT X = 0.5
    FLOAT Y = 0.5
    FLOAT W = 0.1
    FLOAT H = 0.1
ENDSTRUCT

RECT_DETAILS myRect

PROC MAIN()
    
    VEC3 playerPos = <<1.0, 2.0, 3.0>>
    
    VEC2 rectPos = <<0.5, 0.5>>
    
    PED_INDEX playerPed 
    playerPed = <<1>>

    INT a = 5
    INT b = 10
    INT c = a + b * b
    c = (a + b) * b
    c = a + (b * b)

    WHILE TRUE
        WAIT(0)

        DRAW_RECT(rectPos.X, rectPos.Y, 0.1, 0.1, 255, 0, 0, 255, FALSE)
        DRAW_SOMETHING(0, 255, 0)
    ENDWHILE
ENDPROC

PROC DRAW_SOMETHING(INT r, INT g, INT b)
    DRAW_RECT(0.1, 0.1, 0.2, 0.2, r, g, b, 255, FALSE)
ENDPROC
";

        public static void DoTest()
        {
            AntlrInputStream inputStream = new AntlrInputStream(Code);

            ScLangLexer lexer = new ScLangLexer(inputStream);
            CommonTokenStream tokens = new CommonTokenStream(lexer);
            ScLangParser parser = new ScLangParser(tokens);

            Root root = (Root)parser.script().Accept(new AstBuilder());

            Console.WriteLine();
            Console.WriteLine(AstDotGenerator.Generate(root));
            Console.WriteLine();
            Console.WriteLine("===========================");

            root.Accept(new SimpleVisitorTest());
        }

        private sealed class SimpleVisitorTest : AstVisitor
        {
            private readonly HashSet<string> foundTypes = new HashSet<string>();

            public override void VisitType(Ast.Type node)
            {
                string typeName = node.Name.Name;
                if (foundTypes.Add(typeName))
                {
                    Console.WriteLine($"{typeName}\t{node.Source}");
                }
            }

            public override void VisitStructStatement(StructStatement node)
            {
                string structName = node.Name.Name;
                if (foundTypes.Add(structName))
                {
                    Console.WriteLine($"{structName}\t{node.Source}");
                }

                DefaultVisit(node);
            }

            public override void DefaultVisit(Node node)
            {
                foreach (var n in node.Children)
                {
                    Visit(n);
                }
            }
        }
    }
}
