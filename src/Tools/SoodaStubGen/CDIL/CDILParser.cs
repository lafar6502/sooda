using System;
using System.CodeDom;

namespace Sooda.StubGen.CDIL
{
	public class CDILParser : CDILTokenizer
	{
		public CDILParser(string txt) : base(txt)
		{
		}

        public CodeExpression ParseBaseExpression()
        {
            if (TokenType == CDILToken.Integer)
            {
                CodePrimitiveExpression expr = new CodePrimitiveExpression(TokenValue);
                GetNextToken();
                return expr;
            }
            if (TokenType == CDILToken.String)
            {
                CodePrimitiveExpression expr = new CodePrimitiveExpression(TokenValue);
                GetNextToken();
                return expr;
            }
            if (TokenType == CDILToken.Base)
            {
                GetNextToken();
                return new CodeBaseReferenceExpression();
            }

            if (IsKeyword("false"))
            {
                GetNextToken();
                return new CodePrimitiveExpression(false);
            }

            if (IsKeyword("true"))
            {
                GetNextToken();
                return new CodePrimitiveExpression(true);
            }

            if (TokenType == CDILToken.This)
            {
                GetNextToken();
                return new CodeThisReferenceExpression();
            }

            if (TokenType == CDILToken.Arg)
            {
                GetNextToken();
                Expect(CDILToken.LeftParen);
                string name = EatKeyword();
                Expect(CDILToken.RightParen);
                return new CodeArgumentReferenceExpression(name);
            }

            if (IsKeyword("typeref"))
            {
                GetNextToken();
                Expect(CDILToken.LeftParen);
                CodeTypeReference typeRef = ParseType();
                Expect(CDILToken.RightParen);
                return new CodeTypeReferenceExpression(typeRef);
            }

            if (IsKeyword("typeof"))
            {
                GetNextToken();
                Expect(CDILToken.LeftParen);
                CodeTypeReference typeRef = ParseType();
                Expect(CDILToken.RightParen);
                return new CodeTypeOfExpression(typeRef);
            }

            if (IsKeyword("var"))
            {
                GetNextToken();
                Expect(CDILToken.LeftParen);
                string name = EatKeyword();
                Expect(CDILToken.RightParen);
                return new CodeVariableReferenceExpression(name);
            }

            if (IsKeyword("thistype"))
            {
                GetNextToken();
                return null;
            }

            if (TokenType == CDILToken.Cast)
            {
                GetNextToken();
                Expect(CDILToken.LeftParen);
                string typeName = EatKeyword();
                Expect(CDILToken.Comma);
                CodeExpression expr = ParseExpression();
                Expect(CDILToken.RightParen);
                return new CodeCastExpression(typeName, expr);
            }
            if (TokenType == CDILToken.New)
            {
                GetNextToken();
                CodeTypeReference type = ParseType();
                CodeObjectCreateExpression retval = new CodeObjectCreateExpression(type);
                Expect(CDILToken.LeftParen);
                while (TokenType != CDILToken.RightParen && TokenType != CDILToken.EOF)
                {
                    CodeExpression expr = ParseExpression();
                    retval.Parameters.Add(expr);
                    if (TokenType == CDILToken.Comma)
                    {
                        GetNextToken();
                    }
                }
                Expect(CDILToken.RightParen);
                return retval;
            }

            throw BuildException("Unexpected token '" + TokenType + "'");
        }

        public CodeExpression ParseExpression()
        {
            CodeExpression currentValue = ParseBaseExpression();
            while (TokenType == CDILToken.Dot)
            {
                GetNextToken();
                string name = EatKeyword();
                if (TokenType == CDILToken.LeftParen)
                {
                    CodeMethodInvokeExpression methodInvoke = new CodeMethodInvokeExpression(currentValue, name);
                    GetNextToken();
                    while (TokenType != CDILToken.RightParen && TokenType != CDILToken.EOF)
                    {
                        CodeExpression expr = ParseExpression();
                        methodInvoke.Parameters.Add(expr);
                        if (TokenType == CDILToken.Comma)
                        {
                            GetNextToken();
                        }
                    }
                    Expect(CDILToken.RightParen);
                    currentValue = methodInvoke;
                    continue;
                } 
                else if (TokenType == CDILToken.Dollar)
                {
                    GetNextToken();
                    currentValue = new CodeFieldReferenceExpression(currentValue, name);
                    continue;
                }
                else
                {
                    currentValue = new CodePropertyReferenceExpression(currentValue, name);
                }
            }
            return currentValue;
        }

        public CodeTypeReference ParseType()
        {
            string name = this.EatKeyword();
            if (name == "arrayof")
            {
                Expect(CDILToken.LeftParen);
                CodeTypeReference arrayItemType = ParseType();
                Expect(CDILToken.RightParen);
                return new CodeTypeReference(arrayItemType, 1);
            }
            while (TokenType == CDILToken.Dot)
            {
                GetNextToken();
                name += "." + this.EatKeyword();
            }

            return new CodeTypeReference(name);
        }

        public MemberAttributes ParseMemberAttributes()
        {
            MemberAttributes retval = (MemberAttributes)0;

            while (true)
            {
                string keyword = EatKeyword();
                MemberAttributes v = (MemberAttributes)Enum.Parse(typeof(MemberAttributes), keyword, true);
                retval |= v;
                if (TokenType == CDILToken.Comma)
                {
                    GetNextToken();
                    continue;
                }
                else
                {
                    break;
                }
            }

            return retval;
        }

        public CodeTypeMember ParseMember()
        {
            if (IsKeyword("constructor"))
                return ParseConstructor();
            if (IsKeyword("method"))
                return ParseMethod();
            if (IsKeyword("field"))
                return ParseField();
            if (IsKeyword("property"))
                return ParseProperty();
            throw BuildException("Unknown member: " + TokenType);
        }

        public CodeMemberField ParseField()
        {
            CodeMemberField field = new CodeMemberField();
            ExpectKeyword("field");
            field.Type = ParseType();
            field.Name = EatKeyword();
            if (IsKeyword("attributes"))
            {
                GetNextToken();
                field.Attributes = ParseMemberAttributes();
            }
            if (IsKeyword("value"))
            {
                GetNextToken();
                field.InitExpression = ParseExpression();
            }
            ExpectKeyword("end");
            return field;
        }

        public CodeMemberProperty ParseProperty()
        {
            CodeMemberProperty property = new CodeMemberProperty();
            ExpectKeyword("property");
            property.Type = ParseType();
            property.Name = EatKeyword();
            if (TokenType == CDILToken.LeftParen)
            {
                Expect(CDILToken.LeftParen);
                while (TokenType != CDILToken.RightParen && TokenType != CDILToken.EOF)
                {
                    CodeTypeReference typeName = ParseType();
                    string varName = EatKeyword();
                    property.Parameters.Add(new CodeParameterDeclarationExpression(typeName, varName));
                    if (TokenType == CDILToken.Comma)
                        GetNextToken();
                }
                Expect(CDILToken.RightParen);
            }
            if (IsKeyword("attributes"))
            {
                GetNextToken();
                property.Attributes = ParseMemberAttributes();
            }
            if (IsKeyword("get"))
            {
                GetNextToken();
                property.HasGet = true;
                while (!IsKeyword("end") && !IsKeyword("set") && TokenType != CDILToken.EOF)
                {
                    property.GetStatements.Add(ParseStatement());
                    if (TokenType == CDILToken.Semicolon)
                        Expect(CDILToken.Semicolon);
                    else
                        break;
                }
            }
            if (IsKeyword("set"))
            {
                GetNextToken();
                property.HasSet = true;
                while (!IsKeyword("end") && TokenType != CDILToken.EOF)
                {
                    property.SetStatements.Add(ParseStatement());
                    if (TokenType == CDILToken.Semicolon)
                        Expect(CDILToken.Semicolon);
                    else
                        break;
                }
            }
            ExpectKeyword("end");
            return property;
        }
        
        public CodeMemberMethod ParseMethod()
        {
            CodeMemberMethod method = new CodeMemberMethod();

            ExpectKeyword("method");
            string methodName = EatKeyword();
            method.Name = methodName;
            Expect(CDILToken.LeftParen);
            while (TokenType != CDILToken.RightParen && TokenType != CDILToken.EOF)
            {
                CodeTypeReference typeName = ParseType();
                string varName = EatKeyword();
                method.Parameters.Add(new CodeParameterDeclarationExpression(typeName, varName));
                if (TokenType == CDILToken.Comma)
                    GetNextToken();
            }
            Expect(CDILToken.RightParen);
            while (!IsKeyword("begin") && TokenType != CDILToken.EOF)
            {
                if (IsKeyword("returns"))
                {
                    GetNextToken();
                    method.ReturnType = ParseType();
                    continue;
                }
                if (IsKeyword("implements"))
                {
                    GetNextToken();
                    method.ImplementationTypes.Add(ParseType());
                    continue;
                }
                if (IsKeyword("implementsprivate"))
                {
                    GetNextToken();
                    method.PrivateImplementationType = ParseType();
                    continue;
                }
                if (IsKeyword("attributes"))
                {
                    GetNextToken();
                    method.Attributes = ParseMemberAttributes();
                    continue;
                }
                throw BuildException("Unknown keyword: " + TokenValue);
            }

            ExpectKeyword("begin");
            while (!IsKeyword("end") && TokenType != CDILToken.EOF)
            {
                method.Statements.Add(ParseStatement());
                if (TokenType == CDILToken.Semicolon)
                    Expect(CDILToken.Semicolon);
                else
                    break;
            }
            ExpectKeyword("end");

            return method;
        }

        public CodeConstructor ParseConstructor()
        {
            CodeConstructor ctor = new CodeConstructor();

            ExpectKeyword("constructor");
            Expect(CDILToken.LeftParen);
            while (TokenType != CDILToken.RightParen && TokenType != CDILToken.EOF)
            {
                CodeTypeReference typeName = ParseType();
                string varName = EatKeyword();
                ctor.Parameters.Add(new CodeParameterDeclarationExpression(typeName, varName));
                if (TokenType == CDILToken.Comma)
                    GetNextToken();
            }
            Expect(CDILToken.RightParen);
            while (!IsKeyword("begin") && TokenType != CDILToken.EOF)
            {
                if (IsKeyword("attributes"))
                {
                    GetNextToken();
                    ctor.Attributes = ParseMemberAttributes();
                    continue;
                }
                if (IsKeyword("baseArg"))
                {
                    GetNextToken();
                    Expect(CDILToken.LeftParen);
                    ctor.BaseConstructorArgs.Add(ParseExpression());
                    Expect(CDILToken.RightParen);
                    continue;
                }
                if (IsKeyword("chainedArg"))
                {
                    GetNextToken();
                    Expect(CDILToken.LeftParen);
                    ctor.ChainedConstructorArgs.Add(ParseExpression());
                    Expect(CDILToken.RightParen);
                }
                throw BuildException("Unknown keyword: " + TokenValue);
            }

            ExpectKeyword("begin");
            while (!IsKeyword("end") && TokenType != CDILToken.EOF)
            {
                ctor.Statements.Add(ParseStatement());
                if (TokenType == CDILToken.Semicolon)
                    Expect(CDILToken.Semicolon);
                else
                    break;
            }
            ExpectKeyword("end");

            return ctor;
        }

        public CodeTypeDeclaration ParseClass()
        {
            ExpectKeyword("class");
            CodeTypeReference className = ParseType();
            CodeTypeDeclaration ctd = new CodeTypeDeclaration(className.BaseType);
            if (IsKeyword("extends"))
            {
                GetNextToken();
                ctd.BaseTypes.Add(ParseType());
            }
            else
            {
                ctd.BaseTypes.Add(typeof(object));
            }
            while (IsKeyword("implements"))
            {
                GetNextToken();
                ctd.BaseTypes.Add(ParseType());
            }

            ctd.Members.AddRange(ParseMembers());

            return ctd;
        }

        public CodeTypeMemberCollection ParseMembers()
        {
            CodeTypeMemberCollection mc = new CodeTypeMemberCollection();

            while (TokenType != CDILToken.EOF && !IsKeyword("end"))
            {
                CodeTypeMember member = ParseMember();
                mc.Add(member);
            }
            return mc;
        }

        public CodeStatement ParseStatement()
        {
            if (IsKeyword("let"))
            {
                GetNextToken();
                CodeTypeReference type = ParseType();
                string name = EatKeyword();
                if (TokenType == CDILToken.Assign)
                {
                    Expect(CDILToken.Assign);
                    CodeExpression expr = ParseExpression();
                    return new CodeVariableDeclarationStatement(type, name, expr);
                }
                else
                {
                    return new CodeVariableDeclarationStatement(type, name);
                }

            }
            if (IsKeyword("call"))
            {
                GetNextToken();
                return new CodeExpressionStatement(ParseExpression());
            }

            if (IsKeyword("return"))
            {
                CodeMethodReturnStatement retVal;

                GetNextToken();
                retVal = new CodeMethodReturnStatement();
                if (TokenType != CDILToken.Semicolon && TokenType != CDILToken.EOF)
                    retVal.Expression = ParseExpression();
                return retVal;
            }
            if (IsKeyword("throw"))
            {
                CodeThrowExceptionStatement retVal;

                GetNextToken();
                retVal = new CodeThrowExceptionStatement();
                if (TokenType != CDILToken.Semicolon && TokenType != CDILToken.EOF)
                    retVal.ToThrow = ParseExpression();
                return retVal;
            }
            throw BuildException("Invalid token: '" + TokenType + "': " + TokenValue);
        }

        public static CodeStatement ParseStatement(string s, CDILContext context)
        {
            CDILParser parser = new CDILParser(context.Format(s));
            return parser.ParseStatement();
        }

        public static CodeTypeMember ParseMember(string s, CDILContext context)
        {
            CDILParser parser = new CDILParser(context.Format(s));
            return parser.ParseMember();
        }

        public static CodeTypeMemberCollection ParseMembers(string s, CDILContext context)
        {
            CDILParser parser = new CDILParser(context.Format(s));
            return parser.ParseMembers();
        }

        public static CodeTypeDeclaration ParseClass(string s, CDILContext context)
        {
            // Console.WriteLine(s, par);
            CDILParser parser = new CDILParser(context.Format(s));
            return parser.ParseClass();
        }
    }
}