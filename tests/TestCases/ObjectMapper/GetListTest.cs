//
// Copyright (c) 2003-2006 Jaroslaw Kowalski <jaak@jkowalski.net>
// Copyright (c) 2006-2014 Piotr Fusik <piotr@fusik.info>
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

using NUnit.Framework;
using Sooda.UnitTests.BaseObjects;
using System;

namespace Sooda.UnitTests.TestCases.ObjectMapper
{
    [TestFixture]
    public class GetListTest
    {
        [Test]
        public void DoGetListTest()
        {
            using (TestSqlDataSource testDataSource = new TestSqlDataSource("default"))
            {
                testDataSource.Open();

                using (SoodaTransaction tran = new SoodaTransaction())
                {
                    tran.RegisterDataSource(testDataSource);
                    ContactTypeList ctl = ContactType.GetList(tran, SoodaWhereClause.Unrestricted, SoodaSnapshotOptions.Default);
                    foreach (ContactType ct in ctl)
                    {
                        Console.Out.WriteLine("ContactType[{0}]", ct.GetPrimaryKeyValue());
                    }

                    RoleList rl = Role.GetList(tran, SoodaWhereClause.Unrestricted);
                    foreach (Role r in rl)
                    {
                        Console.Out.WriteLine("Role[{0}]", r.GetPrimaryKeyValue());
                    }
                    tran.Commit();
                }
            }
        }

        [Test]
        public void OrderByTest()
        {
            using (TestSqlDataSource testDataSource = new TestSqlDataSource("default"))
            {
                testDataSource.Open();

                using (SoodaTransaction tran = new SoodaTransaction())
                {
                    tran.RegisterDataSource(testDataSource);
                    ContactList l;

                    Contact.Mary.Name = "Aaa";  // to make it temporarily first
                    Contact.Ed.Name = "ZZZZ";   // to make it temporarily last

                    l = Contact.GetList(tran, SoodaWhereClause.Unrestricted, SoodaOrderBy.Ascending("Name"));

                    foreach (Contact c in l)
                    {
                        Console.WriteLine("c: {0}", c.Name);
                    }

                    Assert.IsTrue(l.IndexOf(Contact.Mary) == 0);
                    Assert.IsTrue(l.IndexOf(Contact.Ed) == l.Count - 1);
                    for (int i = 0; i < l.Count - 1; ++i)
                    {
                        if (String.CompareOrdinal((string)l[i].Name, (string)l[i + 1].Name) > 0)
                            Assert.Fail("Invalid sort!");
                    }
                    tran.Commit();
                }
            }
        }

        [Test]
        public void OrderByTest3()
        {
            using (TestSqlDataSource testDataSource = new TestSqlDataSource("default"))
            {
                testDataSource.Open();

                using (SoodaTransaction tran = new SoodaTransaction())
                {
                    tran.RegisterDataSource(testDataSource);
                    ContactList l;

                    l = Contact.GetList(tran, new SoodaWhereClause("Name like '%customer'"), SoodaOrderBy.Ascending("Name"));

                    foreach (Contact c in l)
                    {
                        Console.WriteLine("c: {0}", c.Name);
                    }

                    Assert.IsTrue(l.IndexOf(Contact.Mary) == -1);
                    Assert.IsTrue(l.IndexOf(Contact.Ed) == -1);
                    for (int i = 0; i < l.Count - 1; ++i)
                    {
                        if (String.CompareOrdinal((string)l[i].Name, (string)l[i + 1].Name) > 0)
                            Assert.Fail("Invalid sort!");
                    }
                    tran.Commit();
                }
            }
        }

        [Test]
        public void TestInsertedObject()
        {
            using (SoodaTransaction tran = new SoodaTransaction())
            {
                Contact c0 = new Contact();
                c0.Type = ContactType.Employee;

                ContactList cl = Contact.GetList(new SoodaWhereClause("Name = {0}", "Ala"));
                foreach (Contact c in cl)
                {
                    Console.WriteLine("c.Name = {0}", c.Name);
                }
            }
        }
    }
}
