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
using System.Data;
using System.Collections;

using Sooda.Schema;

using Sooda.Collections;
using Sooda.QL;
using Sooda.Caching;
using Sooda.Logging;

namespace Sooda.ObjectMapper
{
    public class SoodaObjectListSnapshot : ISoodaObjectList
    {
        private static Logger logger = LogManager.GetLogger("SoodaObjectListSnapshot");

        public SoodaObjectListSnapshot()
        {
        }

        public SoodaObjectListSnapshot(IList list)
        {
            foreach (SoodaObject o in list)
            {
                AddObjectToSnapshot(o);
            }
        }

        public SoodaObjectListSnapshot(IList list, SoodaObjectFilter filter)
        {
            foreach (SoodaObject o in list)
            {
                if (filter(o))
                    AddObjectToSnapshot(o);
            }
        }

        public SoodaObjectListSnapshot(IList list, SoodaWhereClause whereClause)
        {
            foreach (SoodaObject o in list)
            {
                if (whereClause.Matches(o, true))
                    AddObjectToSnapshot(o);
            }
        }

        public SoodaObjectListSnapshot(IList list, int first, int length)
        {
            this.classInfo = null;
            items.Capacity = length;

            int start = first;
            if (start < 0)
            {
                length += start;
                start = 0;
            };
            if (start + length > list.Count)
                length = list.Count - start;

            for (int i = 0; i < length; ++i)
            {
                items.Add(list[start + i]);
            }
        }

        public SoodaObjectListSnapshot(IList list, IComparer comp)
        {
            items.Capacity = list.Count;
            for (int i = 0; i < list.Count; ++i)
            {
                items.Add(list[i]);
            }
            items.Sort(comp);
        }

        public SoodaObjectListSnapshot(SoodaTransaction tran, SoodaObjectFilter filter, ClassInfo ci)
        {
            this.classInfo = ci;
            WeakSoodaObjectCollection al = tran.GetObjectsByClassName(ci.Name);

            if (al != null)
            {
                // al.Clone() is needed because
                // the filter expression may materialize new objects
                // during checking. This way we avoid "collection modified" exception

                SoodaObjectCollection clonedArray = new SoodaObjectCollection();
                foreach (WeakSoodaObject wr in al)
                {
                    SoodaObject obj = wr.TargetSoodaObject;
                    if (obj != null)
                    {
                        clonedArray.Add(obj);
                    }
                }

                foreach (SoodaObject obj in clonedArray)
                {
                    if (filter(obj))
                    {
                        items.Add(obj);
                    }
                }
            }
        }

        protected void AddObjectToSnapshot(SoodaObject o)
        {
            items.Add(o);
        }

        public SoodaObjectListSnapshot(SoodaTransaction t, SoodaWhereClause whereClause, SoodaOrderBy orderBy, int topCount, SoodaSnapshotOptions options, ClassInfo ci)
        {
            this.classInfo = ci;
            ClassInfoCollection involvedClasses = null;

            bool useCache = ci.CacheCollections;

            if ((options & SoodaSnapshotOptions.Cache) != 0)
                useCache = true;
            if ((options & SoodaSnapshotOptions.NoCache) != 0)
                useCache = false;

            if (whereClause != null && whereClause.WhereExpression != null)
            {
                if (((options & SoodaSnapshotOptions.NoWriteObjects) == 0)
                    || useCache)
                {
                    try
                    {
                        GetInvolvedClassesVisitor gic = new GetInvolvedClassesVisitor(classInfo);
                        gic.GetInvolvedClasses(whereClause.WhereExpression);
                        involvedClasses = gic.Results;
                    }
                    catch
                    {
                        // logger.Warn("{0}", ex);
                        // cannot detect involved classes (probably because of RAWQUERY)
                        // - precommit all objects
                        // if we get here, involvedClasses remains set to null
                    }
                }
            }
            else
            {
                // no where clause

                involvedClasses = new ClassInfoCollection();
                involvedClasses.Add(ci);
            }

            if ((options & SoodaSnapshotOptions.NoWriteObjects) == 0)
            {
                if (involvedClasses != null)
                {
                    SoodaObjectCollection objectsToPrecommit = new SoodaObjectCollection();

                    // precommit objects

                    foreach (ClassInfo involvedClass in involvedClasses)
                    {
                        WeakSoodaObjectCollection dirtyObjects = t.GetDirtyObjectsByClassName(involvedClass.Name);
                        if (dirtyObjects != null)
                        {
                            foreach (WeakSoodaObject wr in dirtyObjects)
                            {
                                SoodaObject o = wr.TargetSoodaObject;
                                if (o != null)
                                    objectsToPrecommit.Add(o);
                            }
                        }
                    }
                    t.SaveObjectChanges(true, objectsToPrecommit);
                }
                else
                {
                    // don't know what to precommit - precommit everything
                    t.SaveObjectChanges(true, null);
                }
            }

            LoadList(t, whereClause, orderBy, topCount, options, involvedClasses, useCache);
        }

        private void LoadList(SoodaTransaction t, SoodaWhereClause whereClause, SoodaOrderBy orderBy, int topCount, SoodaSnapshotOptions options, ClassInfoCollection involvedClasses, bool useCache)
        {
            SoodaTransaction transaction = t;

            ISoodaObjectFactory factory = t.GetFactory(classInfo);
            SoodaCachedCollectionKey cacheKey = null;

            if (useCache)
            {
                // cache makes sense only on clean database
                if (!t.HasBeenPrecommitted(classInfo))
                {
                    cacheKey = SoodaCache.GetCollectionKey(classInfo, whereClause);
                }

                IEnumerable keysCollection = SoodaCache.LoadCollection(cacheKey);
                if (keysCollection != null)
                {
                    SoodaStatistics.Global.RegisterCollectionCacheHit();
                    t.Statistics.RegisterCollectionCacheHit();
                    foreach (object o in keysCollection)
                    {
                        SoodaObject obj = factory.GetRef(t, o);
                        items.Add(obj);
                    }

                    if (orderBy != null)
                    {
                        items.Sort(orderBy.GetComparer());
                    }

                    if (topCount != -1 && topCount < items.Count)
                    {
                        items.RemoveRange(topCount, items.Count - topCount);
                    }

                    return;
                }
                else
                {
                    // logger.Debug("Collection cache miss: {0}", cacheKey);
                    SoodaStatistics.Global.RegisterCollectionCacheMiss();
                    t.Statistics.RegisterCollectionCacheMiss();
                }
            }

            if ((options & SoodaSnapshotOptions.NoDatabase) == 0)
            {
                SoodaDataSource ds = transaction.OpenDataSource(classInfo.GetDataSource());

                if ((options & SoodaSnapshotOptions.KeysOnly) != 0)
                {
                    using (IDataReader reader = ds.LoadMatchingPrimaryKeys(t.Schema, classInfo, whereClause, orderBy, topCount))
                    {
                        while (reader.Read())
                        {
                            SoodaObject obj = SoodaObject.GetRefFromKeyRecordHelper(transaction, factory, reader);
                            items.Add(obj);
                        }
                    }
                }
                else
                {
                    TableInfo[] loadedTables;

                    using (IDataReader reader = ds.LoadObjectList(t.Schema, classInfo, whereClause, orderBy, topCount, options, out loadedTables))
                    {
                        while (reader.Read())
                        {
                            SoodaObject obj = SoodaObject.GetRefFromRecordHelper(transaction, factory, reader, 0, loadedTables, 0);
                            if ((options & SoodaSnapshotOptions.VerifyAfterLoad) != 0)
                            {
                                if (whereClause != null && !whereClause.Matches(obj, false))
                                {
                                    // don't add the object
                                    continue;
                                }
                            }
                            items.Add(obj);
                        }
                    }
                }

                if (cacheKey != null && useCache && (topCount == -1) && (involvedClasses != null))
                {
                    object[] keys = new object[items.Count];
                    int p = 0;

                    foreach (SoodaObject obj in items)
                    {
                        keys[p++] = obj.GetPrimaryKeyValue();
                    }
                    SoodaCache.StoreCollection(cacheKey, keys, involvedClasses);
                }
            }
        }

        public SoodaObject GetItem(int pos)
        {
            return (SoodaObject)items[pos];
        }

        public int Add(object obj)
        {
            return items.Add(obj);
        }

        public void Remove(object obj)
        {
            items.Remove(obj);
        }

        public bool Contains(object obj)
        {
            return items.Contains(obj);
        }

        public IEnumerator GetEnumerator()
        {
            return items.GetEnumerator();
        }

        private ArrayList items = new ArrayList();
        private ClassInfo classInfo;

        public bool IsReadOnly
        {
            get
            {
                return true;
            }
        }

        object IList.this[int index]
        {
            get
            {
                return GetItem(index);
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public void RemoveAt(int index)
        {
            Remove(GetItem(index));
        }

        public void Insert(int index, object value)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            items.Clear();
        }

        public int IndexOf(object value)
        {
            return items.IndexOf(value);
        }

        public bool IsFixedSize
        {
            get
            {
                return false;
            }
        }

        public bool IsSynchronized
        {
            get
            {
                return false;
            }
        }

        public int Count
        {
            get
            {
                return items.Count;
            }
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public object SyncRoot
        {
            get
            {
                return this;
            }
        }

        public ISoodaObjectList GetSnapshot()
        {
            return this;
        }

        public ISoodaObjectList SelectFirst(int n)
        {
            return new SoodaObjectListSnapshot(this, 0, n);
        }

        public ISoodaObjectList SelectLast(int n)
        {
            return new SoodaObjectListSnapshot(this, this.Count - n, n);
        }

        public ISoodaObjectList SelectRange(int from, int to)
        {
            return new SoodaObjectListSnapshot(this, from, to - from);
        }

        public ISoodaObjectList Filter(SoodaObjectFilter filter)
        {
            return new SoodaObjectListSnapshot(this, filter);
        }

        public ISoodaObjectList Filter(SoodaWhereClause whereClause)
        {
            return new SoodaObjectListSnapshot(this, whereClause);
        }

        public ISoodaObjectList Sort(IComparer comparer)
        {
            return new SoodaObjectListSnapshot(this, comparer);
        }
    }
}
