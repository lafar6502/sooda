//
// Copyright (c) 2002-2004 Jaroslaw Kowalski <jaak@polbox.com>
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
// * Neither the name of the Jaroslaw Kowalski nor the names of its
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
using System.Diagnostics;
using System.Data;
using Sooda.Sql;
using System.IO;
using Sooda.UnitTests.Objects;

using NUnit.Framework;

namespace Sooda.UnitTests.TestCases.SqlBuilder {
    [TestFixture]
    public class CommandParamTest {
        [Test]
        public void ParameterDirectionTest() {
            using(IDbCommand cmd = new System.Data.SqlClient.SqlCommand()) {
                ISqlBuilder bld = new SqlServerBuilder();
                String sql = "exec sp_Test({0}, {1:i}, {2:o}, {3:io}, {4:i}, {5}, {6}, {7}, {8}, {9}, {10:i}, {11:io})";
                Object[] param = new Object[]{ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
                bld.BuildCommandWithParameters(cmd, sql, param);
                Assertion.AssertEquals(((IDataParameter)cmd.Parameters[0]).Direction, ParameterDirection.Input);
                Assertion.AssertEquals(((IDataParameter)cmd.Parameters[1]).Direction, ParameterDirection.Input);
                Assertion.AssertEquals(((IDataParameter)cmd.Parameters[2]).Direction, ParameterDirection.Output);
                Assertion.AssertEquals(((IDataParameter)cmd.Parameters[3]).Direction, ParameterDirection.InputOutput);
                Assertion.AssertEquals(((IDataParameter)cmd.Parameters[10]).Direction, ParameterDirection.Input);
                Assertion.AssertEquals(((IDataParameter)cmd.Parameters[11]).Direction, ParameterDirection.InputOutput);
            }
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ParameterDirectionFailTest() {
            using(IDbCommand cmd = new System.Data.SqlClient.SqlCommand()) {
                ISqlBuilder bld = new SqlServerBuilder();
                String sql = "exec sp_Test({0:})";
                Object[] param = new Object[]{ 0, 1, 2, 3 };
                bld.BuildCommandWithParameters(cmd, sql, param);
            }
        }

    }
}
