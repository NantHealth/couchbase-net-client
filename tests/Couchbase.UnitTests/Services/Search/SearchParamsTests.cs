﻿using System;
using System.Collections.Generic;
using Couchbase.Services.Search;
using Couchbase.Services.Search.Queries.Simple;
using Couchbase.Services.Search.Sort;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Couchbase.UnitTests.Services.Search
{
    public class SearchParamsTests
    {
        [Fact]
        public void ToJson_JsonStringIsValid()
        {
            var searchParams = new SearchQuery().
                Skip(20).
                Limit(10).Explain(true).
                Timeout(TimeSpan.FromMilliseconds(10000)).
#pragma warning disable 618
                WithConsistency(ScanConsistency.AtPlus);
#pragma warning restore 618

            //var expected = "{\"ctl\":{\"timeout\":10000,\"consistency\":{\"level\":\"at_plus\",\"vectors\":{\"customerIndex\":{\"0\":123,\"1/a0b1c2\":234}}}},\"query\":{\"query\":\"alice smith\",\"boost\": 1},\"size\": 10,\"from\":20,\"highlight\":{\"style\": null,\"fields\":null},\"fields\":[\"*\"],\"facets\":null,\"explain\":true}";
            var expected = "{\"ctl\":{\"timeout\":10000,\"consistency\":{\"level\":\"at_plus\",\"vectors\":{}}},\"size\":10,\"from\":20,\"explain\":true}";
            var actual = searchParams.ToJson().ToString().Replace("\r\n", "").Replace(" ", "");
            Console.WriteLine(actual);
            Console.WriteLine(expected);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ToJson_WithFacets()
        {
            var searchParams = new SearchQuery().Facets(
                new TermFacet("termfacet", "thefield", 10),
                new DateRangeFacet("daterangefacet", "thefield", 10).AddRange(DateTime.Now, DateTime.Now.AddDays(1)),
                new NumericRangeFacet("numericrangefacet", "thefield", 2).AddRange(2.2f, 3.5f));

            Console.WriteLine(searchParams.ToJson());
        }

        [Fact]
        public void Test_SupportsFields()
        {
            //will not throw ArgumentNullException
            SearchQuery fc = new SearchQuery();
            fc.Index = "beer-ft";
            fc.Highlighting(HighLightStyle.Html);
            fc.Fields("name", "style");
        }

        [Fact]
        public void Test_Fields_WhenNull_ThrowsException()
        {
            SearchQuery fc = new SearchQuery();
            fc.Index = "beer-ft";
            fc.Highlighting(HighLightStyle.Html);
            Assert.Throws<ArgumentNullException>(() => fc.Fields(null));
        }

        [Fact]
        public void Test_Fields_WhenEmpty_ThrowsException()
        {
            SearchQuery fc = new SearchQuery();
            fc.Index = "beer-ft";
            fc.Highlighting(HighLightStyle.Html);
            Assert.Throws<ArgumentNullException>(() => fc.Fields());
        }

        [Fact]
        public void Test_HighLightStyle_Html_And_Fields_Returns_LowerCase()
        {
            var query = new SearchQuery
            {
                Index = "idx_travel",
                Query = new MatchQuery("inn")
            }.Highlighting(HighLightStyle.Html, "inn");

            var result = query.ToJson();
            var expected = JsonConvert.SerializeObject(new
            {
                ctl = new
                {
                    timeout = 75000
                },
                highlight = new
                {
                    style="html",
                    fields = new [] {"inn"}
                },
                query=new {match="inn", prefix_length=0, fuzziness=0}
            }, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Sort_Adds_FieldNames_To_Output_Json()
        {
            var fields = new List<string> {"name", "-age"};

            var searchParams = new SearchOptions();
            searchParams.Sort(fields.ToArray());

            var result = searchParams.ToJson().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                ctl = new
                {
                    timeout = 75000
                },
                sort = fields
            }, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Sort_SearchSort_To_Output_Json()
        {
            var searchSort = new IdSearchSort();

            var searchParams = new SearchOptions();
            searchParams.Sort(searchSort);

            var result = searchParams.ToJson().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                ctl = new
                {
                    timeout = 75000
                },
                sort = new[]
                {
                    new
                    {
                        by = "id"
                    }
                }
            }, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Sort_JObject_To_Output_Json()
        {
            var json = new JObject
            {
                new JProperty("foo", "bar")
            };

            var searchParams = new SearchOptions();
            searchParams.Sort(json);

            var result = searchParams.ToJson().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                ctl = new
                {
                    timeout = 75000
                },
                sort = new []
                {
                    new
                    {
                        foo = "bar"
                    }
                }
            }, Formatting.None);

            Assert.Equal(expected, result);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
