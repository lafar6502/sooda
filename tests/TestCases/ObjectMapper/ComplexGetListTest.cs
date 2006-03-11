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
using System.Diagnostics;
using System.Data;

using Sooda.ObjectMapper;
using Sooda.UnitTests.Objects;
using Sooda.UnitTests.BaseObjects;
using Sooda.Caching;

using NUnit.Framework;

namespace Sooda.UnitTests.TestCases.ObjectMapper
{
    [TestFixture]
    public class ComplexGetListTest
    {
        [Test]
        public void Test1()
        {
            using (SoodaTransaction tran = new SoodaTransaction())
            {
                Contact.Mary.Name = "temp1";
                SoodaCache.Dump(Console.Out);

                ContactList cl = Contact.GetList(tran, new SoodaWhereClause("Name = {0}", "Mary Manager"), SoodaOrderBy.Unsorted);
                Assert.AreEqual(0, cl.Count);
            }
        }

        [Test]
        public void Test2()
        {
            using (SoodaTransaction tran = new SoodaTransaction())
            {
                Contact.Mary.Name = "temp1";

                ContactList cl = Contact.GetList(tran, new SoodaWhereClause("PrimaryGroup.Manager.Name = {0}", "Mary Manager"), SoodaOrderBy.Unsorted);
                Assert.AreEqual(0, cl.Count);
            }
        }

        [Test]
        public void Test3()
        {
            using (SoodaTransaction tran = new SoodaTransaction())
            {
                Contact.Mary.Name = "temp1";

                ContactList cl = Contact.GetList(tran, new SoodaWhereClause("PrimaryGroup.Manager.Name = {0}", "temp1"), SoodaOrderBy.Unsorted);
                Assert.AreEqual(4, cl.Count);
            }
        }

        [Test]
        public void Test4()
        {
            using (SoodaTransaction tran = new SoodaTransaction())
            {
                Group.GetRef(10).Name = "temp7";

                ContactList cl = Contact.GetList(tran, new SoodaWhereClause("PrimaryGroup.Name = {0}", "temp7"), SoodaOrderBy.Unsorted);
                Assert.AreEqual(4, cl.Count);
            }
        }

        [Test]
        public void Test5()
        {
            using (SoodaTransaction tran = new SoodaTransaction())
            {
                Contact c = new Contact();
                c.Type = ContactType.Customer;
                c.PrimaryGroup = Group.GetRef(10);

                ContactList cl = Contact.GetList(tran, new SoodaWhereClause("PrimaryGroup.Manager.Name = {0}", "Mary Manager"), SoodaOrderBy.Unsorted);
                Assert.AreEqual(5, cl.Count);
            }
        }

        [Test]
        public void Test6()
        {
            using (SoodaTransaction tran = new SoodaTransaction())
            {
                Contact c = new Contact();
                c.Type = ContactType.Customer;
                c.PrimaryGroup = Group.GetRef(11);
                c.Roles.Add(Role.Employee);
                Contact.Mary.Roles.Remove(Role.Employee);

                Console.WriteLine(tran.Serialize());
                ContactList cl = Contact.GetList(tran, new SoodaWhereClause("Roles.Contains(Role where Name={0})", "Employee"), SoodaOrderBy.Unsorted);
                Assert.AreEqual(3, cl.Count);
                cl = Contact.GetList(tran, new SoodaWhereClause("Roles.Contains(Role where Name={0})", "Employee"), SoodaOrderBy.Unsorted);
                Assert.AreEqual(3, cl.Count);
                Console.WriteLine(tran.Serialize());
            }
        }
    }
}
