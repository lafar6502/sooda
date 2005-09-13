// 
// Copyright (c) 2002-2005 Jaroslaw Kowalski <jkowalski@users.sourceforge.net>
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
// * Neither the name of Jaroslaw Kowalski nor the names of its 
//   contributors may be used to endorse or promote products derived from this
//   software without specific prior written permission. 
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
using System.IO;
using System.Collections;

using System.Xml.Serialization;

namespace Sooda.QL 
{
    public class SoqlBooleanInExpression : SoqlBooleanExpression 
    {
        public SoqlExpression Left;

        [XmlArray("in")]
        [XmlArrayItem("item")]
        public SoqlExpressionCollection Right;

        public SoqlBooleanInExpression() {}
        public SoqlBooleanInExpression(SoqlExpression lhs, SoqlExpressionCollection rhs) 
        {
            this.Left = lhs;
            this.Right = rhs;
        }

        // visitor pattern
        public override void Accept(ISoqlVisitor visitor) 
        {
            visitor.Visit(this);
        }

        public override SoqlExpression Simplify() 
        {
            SoqlExpression lhsExpression = Left.Simplify();
            SoqlBooleanExpression retVal = null;

            for (int i = 0; i < Right.Count; ++i) 
            {
                SoqlExpression e = (SoqlExpression)Right[i];
                SoqlBooleanRelationalExpression bre =
                    new SoqlBooleanRelationalExpression(lhsExpression, e, SoqlRelationalOperator.Equal);

                if (retVal == null)
                    retVal = bre;
                else
                    retVal = new SoqlBooleanOrExpression(retVal, bre);
            }

            return retVal.Simplify();
        }

        public override object Evaluate(ISoqlEvaluateContext context)
        {
            // this should be evaluated on 
            return Simplify().Evaluate(context);
        }
    }
}