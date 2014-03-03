// 
// Copyright (c) 2003-2006 Jaroslaw Kowalski <jaak@jkowalski.net>
// 
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without 
// modification, are permitted provided that the following conditions 
// are met:
// 
// * Redistributions of source code must retain the above copyright notice, 
//   this list of conditions and the following disclaimer. 
// 
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution. 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
// ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF 
// THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;

using Sooda.Schema;

namespace Sooda.Sql
{
    public class PostgreSqlBuilder : SqlBuilderNamedArg
    {
        public override string GetSQLDataType(Sooda.Schema.FieldInfo fi)
        {
            switch (fi.DataType)
            {
                case FieldDataType.Integer:
                    return "int";

                case FieldDataType.String:
                    return "varchar(" + fi.Size + ")";

                case FieldDataType.DateTime:
                    return "datetime";

                default:
                    throw new NotImplementedException(String.Format("Datatype {0} not supported for this database", fi.DataType.ToString()));
            }
        }

        protected override string GetNameForParameter(int pos)
        {
            return ":p" + pos.ToString();
        }

        public override string QuoteFieldName(string s)
        {
            return String.Concat("\"", s, "\"");
        }

        public override SqlTopSupportMode TopSupport
        {
            get
            {
                return SqlTopSupportMode.MySqlLimit;
            }
        }

        public override string GetSQLOrderBy(Sooda.Schema.FieldInfo fi, bool start)
        {
            return "";
        }

    }
}
