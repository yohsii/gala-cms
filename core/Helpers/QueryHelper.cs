﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using puck.core.Abstract;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using Lucene.Net.QueryParsers;
using puck.core.Constants;
using puck.core.Base;
using System.Web;
using System.Globalization;
using Lucene.Net.Search;
using System.Threading;
using puck.core.Models;
using Lucene.Net.Spatial.Queries;
using Spatial4n.Core.Context;
using Spatial4n.Core.Distance;
using Lucene.Net.Spatial;
using Lucene.Net.Spatial.Vector;
using puck.core.State;
using Lucene.Net.QueryParsers.Classic;
using Spatial4n.Core.Shapes;
using Lucene.Net.Queries.Function;
using Lucene.Net.Spatial.Prefix.Tree;
using Lucene.Net.Spatial.Prefix;
using Microsoft.AspNetCore.Localization;
using puck.core.Controllers;

namespace puck.core.Helpers
{
    public static class QueryExtensions {
        //term modifier string extensions
        public static string WildCardSingle(this string s, bool perWord = false)
        {
            if (perWord)
                return string.Join(" ", s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x + "?"));
            else
                return s + "?";
        }

        public static string WildCardMulti(this string s, bool perWord = false)
        {
            if (perWord)
                return string.Join(" ", s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x + "*"));
            else
                return s + "*";
        }

        public static string Fuzzy(this string s, float? fuzzyness = null, bool perWord = false)
        {
            if (perWord)
                return string.Join(" ", s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x + "~" + (fuzzyness.HasValue ? fuzzyness.ToString() : "")));
            else
                return s + "~";
        }

        public static string Boost(this string s, float? boost = null, bool perWord = false)
        {
            if (perWord)
                return string.Join(" ", s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x + "^" + (boost.HasValue ? boost.ToString() : "")));
            else
                return s + "^";
        }

        public static string Proximity(this string s, float? proximity = null)
        {
            return s + "~" + (proximity.HasValue ? proximity.ToString() : "");
        }

        public static string Escape(this string s)
        {
            return QueryParser.Escape(s);
        }

        public static string Wrap(this string s)
        {
            return string.Concat("\"", s, "\"");
        }
        //misc helper extensions
        public static int GetLevel(this BaseModel m)
        {
            int level = m.Path.Count(x => x == '/');
            return level;
        }

        public static List<T> GetAll<T>(this List<PuckReference> pp, bool noCast = false) where T : BaseModel
        {
            if (pp == null)
                return new List<T>();
            var qh = new QueryHelper<T>();
            var qhinner1 = qh.New();
            foreach (var p in pp) {
                var qhinner2 = qhinner1.New().Id(p.Id);
                if (!string.IsNullOrEmpty(p.Variant))
                    qhinner2.Variant(p.Variant.ToLower());
                qhinner1.Group(
                    qhinner2
                );
            }
            qh.And().Group(qhinner1);
            //if (noCast)
            //    return qh.GetAllNoCast();
            //else
            //    return qh.GetAll();

            var sortOrder = new Dictionary<Guid, int>();
            for (var i = 0; i < pp.Count; i++)
            {
                sortOrder[pp[i].Id] = i;
            }
            
            List<T> unsortedResults;
            if (noCast)
                unsortedResults = qh.GetAllNoCast();
            else
                unsortedResults = qh.GetAll();

            var results = unsortedResults.OrderBy(x=>sortOrder[x.Id]).ToList();

            return results;
        }

        public static List<T> GetAll<T>(this PuckReference pp,bool noCast=false) where T : BaseModel
        {
            if (pp == null)
                return new List<T>();
            var qh = new QueryHelper<T>();
            qh.Id(pp.Id);
            if (!string.IsNullOrEmpty(pp.Variant))
                qh.Variant(pp.Variant);
            if (noCast)
                return qh.GetAllNoCast();
            else
                return qh.GetAll();
        }

        public static T Get<T>(this PuckReference pp,bool noCast=false) where T : BaseModel
        {
            return GetAll<T>(pp, noCast).FirstOrDefault();
        }
        //retrieval extensions
        public static List<T> Parent<T>(this BaseModel n,bool currentLanguage = true,bool noCast=false) where T : BaseModel
        {
            if (n.Path.Count(x => x == '/') == 1)
                return new List<T>();
            var qh = new QueryHelper<T>();
            string path = n.Path.Substring(0, n.Path.LastIndexOf('/'));
            qh
                .And()
                //.Field(x => x.Path, path.ToLower());
                .Path(path.ToLower());
            if (currentLanguage)
                qh.CurrentLanguage();
            if (noCast)
                return qh.GetAllNoCast();
            else
                return qh.GetAll();
        }
        public static List<T> Ancestors<T>(this BaseModel n,bool currentLanguage=true,bool noCast = false,bool ExplicitType=false) where T : BaseModel {
            if (n.Path.Count(x => x == '/') == 1)
                return new List<T>();
            var qh = new QueryHelper<T>();
            string nodePath = n.Path.ToLower();
            var innerQ = qh.New();
            while (nodePath.Count(x => x == '/') > 1)
            {
                nodePath = nodePath.Substring(0, nodePath.LastIndexOf('/'));
                innerQ
                    //.Field(x=>x.Path,nodePath);
                    .Path(nodePath);
            }
            qh.And(innerQ);
            if (ExplicitType)
                qh.ExplicitType();
            if (currentLanguage)
                qh.CurrentLanguage();
            if (noCast)
                return qh.GetAllNoCast();
            else
                return qh.GetAll();
        }
        public static List<T> Siblings<T>(this BaseModel n,bool currentLanguage=true,bool noCast=false,bool ExplicitType=false) where T : BaseModel {
            var qh = new QueryHelper<T>();
            qh
                    .And()
                    //.Field(x => x.Path, ApiHelper.DirOfPath(n.Path.ToLower()).WildCardMulti())
                    .Path(ApiHelper.DirOfPath(n.Path.ToLower()).WildCardMulti())
                    .Not()
                    //.Field(x => x.Path, ApiHelper.DirOfPath(n.Path.ToLower()).WildCardMulti() + "/*")
                    .Path(ApiHelper.DirOfPath(n.Path.ToLower()).WildCardMulti() + "/*")
                    .Not()
                    .Field(x => x.Id, n.Id.ToString().Wrap());
            if (ExplicitType)
                qh.ExplicitType();
            if (currentLanguage)
                qh.CurrentLanguage();
            if (noCast)
                return qh.GetAllNoCast();
            else
                return qh.GetAll();                
        }
        public static List<T> Variants<T>(this BaseModel n,bool noCast=false,bool publishedOnly=true) where T : BaseModel
        {
            var qh = new QueryHelper<T>(publishedContentOnly:publishedOnly);
            qh      
                    .And()
                    .Field(x => x.Id, n.Id.ToString())
                    .Not()
                    .Field(x => x.Variant, n.Variant);
            if (noCast)
                return qh.GetAllNoCast();
            else
                return qh.GetAll();
        }
        public static List<T> Children<T>(this BaseModel n,bool currentLanguage=true,bool noCast = false,bool ExplicitType=false) where T : BaseModel
        {
            var qh = new QueryHelper<T>();
            qh      
                    .And()
                    //.Field(x => x.Path, n.Path.ToLower() + "/".WildCardMulti())
                    .Path(n.Path.ToLower() + "/".WildCardMulti())
                    .Not()
                    //.Field(x => x.Path, n.Path.ToLower()+"/".WildCardMulti() + "/*");
                    .Path(n.Path.ToLower() + "/".WildCardMulti() + "/*");
            if (ExplicitType)
                qh.ExplicitType();
            if (currentLanguage)
                qh.CurrentLanguage();
            if (noCast)
                return qh.GetAllNoCast();
            else
                return qh.GetAll();
        }
        public static List<T> Descendants<T>(this BaseModel n,bool currentLanguage=true,bool noCast = false,bool ExplicitType=false,bool publishedOnly=true) where T : BaseModel {
            var qh = new QueryHelper<T>(publishedContentOnly:publishedOnly);
            qh.And()
                //.Field(x => x.Path, n.Path.ToLower()+"/".WildCardMulti());
                .Path(n.Path.ToLower() + "/".WildCardMulti());
            if (ExplicitType)
                qh.ExplicitType();
            if (currentLanguage)
                qh.CurrentLanguage();
            if (noCast)
                return qh.GetAllNoCast();
            else
                return qh.GetAll();
        }
        
        public static Dictionary<string, Dictionary<string, T>> GroupByPath<T>(this List<T> items) where T : BaseModel
        {
            var d = new Dictionary<string, Dictionary<string, T>>();
            items.GroupBy(x => x.Path).ToList().ForEach(x =>
            {
                d.Add(x.Key, new Dictionary<string, T>());
                x.ToList().ForEach(y => d[x.Key][y.Variant] = y);
            });
            return d;
        }
        public static Dictionary<Guid, Dictionary<string, T>> GroupById<T>(this List<T> items) where T : BaseModel
        {
            var d = new Dictionary<Guid, Dictionary<string, T>>();
            items.GroupBy(x => x.Id).ToList().ForEach(x =>
            {
                d.Add(x.Key, new Dictionary<string, T>());
                x.ToList().ForEach(y => d[x.Key][y.Variant] = y);
            });
            return d;
        }
    }
    public class QueryHelper<TModel> where TModel : BaseModel
    {
        public static I_Content_Searcher searcher = PuckCache.PuckSearcher;
        public static SpatialContext ctx = SpatialContext.GEO;
        public Lucene.Net.Search.Filter filter;
        //query builders append to this string
        string query = "";
        int totalHits = 0;
        Sort sort = null;
        List<SortField> sorts = null;
        public int TotalHits { get { return totalHits; } }
        static string namePattern = @"(?:[A-Za-z0-9]*\()?[A-Za-z0-9]\.([A-Za-z0-9.]*)";
        static string nameArrayPattern = @"\.get_Item\(\d\)";
        static string paramPattern = @"((?:[a-zA-Z0-9]+\.?)+)\)";
        static string queryPattern = @"^\(*""(.*)""\s";
        static string fieldPattern = @"@";
        static string dateFormat = "yyyyMMddHHmmss";

        //regexes compiled on startup and reused since they will be used frequently
        static Regex nameRegex = new Regex(namePattern, RegexOptions.Compiled);
        static Regex nameArrayRegex = new Regex(nameArrayPattern, RegexOptions.Compiled);
        static Regex paramRegex = new Regex(paramPattern, RegexOptions.Compiled);
        static Regex queryRegex = new Regex(queryPattern, RegexOptions.Compiled);
        static Regex fieldRegex = new Regex(fieldPattern, RegexOptions.Compiled);

        //static helpers
        public static IList<Dictionary<string, string>> Query(string q) {
            return searcher.Query(q);
        }
        public static IList<Dictionary<string, string>> Query(Query q)
        {
            return searcher.Query(q);
        }
        public static string Escape(string q) {
            return QueryParser.Escape(q);
        }
        private static string getName(string str) {
            //((exp.Body as PropertyExpression)).Member.Name
            str = nameArrayRegex.Replace(str, "");
            var match = nameRegex.Match(str);
            string result = match.Groups[1].Value;
            //result = result.ToLower();
            return result;
        }

        public static string GetName<TModel>(Expression<Func<TModel, object>> exp)
        {
            return getName(exp.Body.ToString());
        }

        public static string Format<TModel>(Expression<Func<TModel, object>> exp)
        {
            return Format<TModel>(exp, null);
        }

        public static string Format<TModel>(Expression<Func<TModel, object>> exp, params string[] values)
        {
            values = values.Select(x => x).ToArray();
            string bodystr = exp.Body.ToString();
            var pmatches = paramRegex.Matches(bodystr);
            var qmatch = queryRegex.Matches(bodystr);
            var query = qmatch[0].Groups[1].Value;

            for (var i = 0; i < pmatches.Count; i++)
            {
                var p = pmatches[i].Groups[1].Value;
                p = getName(p);
                query = fieldRegex.Replace(query, p, 1);
            }
            if (values != null)
            {
                query = string.Format(query, values);
            }
            return query;
        }

        public static List<T> GetAll<T>() where T : BaseModel {
            return searcher.Get<T>().ToList();
        }

        public static string PathPrefix() {
            string domain = HttpContext.Current.Request.GetUri().Host.ToLower();
            string searchPathPrefix;
            if (!PuckCache.DomainRoots.TryGetValue(domain, out searchPathPrefix))
            {
                if (!PuckCache.DomainRoots.TryGetValue("*", out searchPathPrefix))
                    throw new Exception("domain roots not set. DOMAIN:" + domain);
            }
            return searchPathPrefix.ToLower();
        }

        public static List<TModel> CurrentAll()
        {
            string absPath = HttpContext.Current.Request.GetUri().AbsolutePath.ToLower();
            string path = PathPrefix() + (absPath == "/" ? "" : absPath);
            var qh = new QueryHelper<TModel>();
            //qh.And().Field(x => x.Path, path);
            qh.And().Path(path);
            return qh.GetAllNoCast();
        }

        public static TModel Current()
        {
            //var requestCultureFeature = HttpContext.Current.Features.Get<IRequestCultureFeature>();
            //var variant = requestCultureFeature.RequestCulture.Culture.Name.ToLower();
            //var variant = CultureInfo.CurrentCulture.Name.ToLower();
            string absPath = HttpContext.Current.Request.GetUri().AbsolutePath.ToLower();
            string path = PathPrefix() + (absPath == "/" ? "" : absPath);

            var currentVariantFromHttpContext = HttpContext.Current?.Items["variant"];
            string v = null;
            if (currentVariantFromHttpContext == null && HttpContext.Current != null)
            {
                v = new BaseController().GetVariant(path);
                HttpContext.Current.Items["variant"] = v;
            }
            else if (currentVariantFromHttpContext != null)
                v = currentVariantFromHttpContext as string;

            var variant = v ?? PuckCache.SystemVariant;
            
            var qh = new QueryHelper<TModel>();
            //qh.And().Field(x => x.Path, path).Variant(variant);
            qh.And().Path(path).Variant(variant);
            return qh.GetNoCast();
        }

        //constructor
        public QueryHelper(bool prependTypeTerm = true,bool publishedContentOnly=true)
        {
            if (prependTypeTerm)
            {
                if (typeof(TModel) == typeof(BaseModel)) {
                    if(publishedContentOnly)
                        this.And().Field(x => x.Published, "true");
                }
                else
                {
                    var innerQ = this.New();
                    foreach (var type in PuckCache.ModelDerivedModels[typeof(TModel).Name])
                    {
                        innerQ.Field(x=>x.Type,type.Name);
                    }
                    this.Must().Group(innerQ);//.And().Field(x => x.Published, "true");
                    if (publishedContentOnly)
                        this.And().Field(x => x.Published, "true");
                }
                //this.And().Field(x => x.TypeChain, typeof(TModel).Name.Wrap()).And().Field(x => x.Published, "true");
            }
        }
        public void SetQuery(string query) {
            this.query = query;
        }
        public QueryHelper<TModel> New() {
            return new QueryHelper<TModel>(prependTypeTerm: false);
        }

        //query builders
        public QueryHelper<TModel> SortByDistanceFromPoint(Expression<Func<TModel, object>> exp, double longitude,double latitude,bool desc=false)
        {
            if (sort == null)
            {
                sort = new Sort();
                sorts = new List<SortField>();
            }
            string key = getName(exp.Body.ToString());
            int maxLevels = 11;
            SpatialPrefixTree grid = new GeohashPrefixTree(ctx, maxLevels);
            var strat = new RecursivePrefixTreeStrategy(grid, key);
            //var strat = new PointVectorStrategy(ctx, key);
            IPoint pt = ctx.MakePoint(longitude, latitude);
            ValueSource valueSource = strat.MakeDistanceValueSource(pt, DistanceUtils.DEG_TO_KM);//the distance (in km)
            sorts.Add(valueSource.GetSortField(!desc));
            sort.SetSort(sorts.ToArray());//.Rewrite(indexSearcher);//false=asc dist
            return this;
        }
        public QueryHelper<TModel> Sort(Expression<Func<TModel, object>> exp, bool descending=false,SortFieldType? sortFieldType=null)
        {
            if (sort == null)
            {
                sort = new Sort();
                sorts = new List<SortField>();
            }
            string key = getName(exp.Body.ToString());
            if (sortFieldType==null){
                sortFieldType = SortFieldType.STRING;
                string fieldTypeName = PuckCache.TypeFields[typeof(TModel).AssemblyQualifiedName][key];
                if (fieldTypeName.Equals(typeof(int).AssemblyQualifiedName))
                {
                    sortFieldType = SortFieldType.INT32;
                }
                else if (fieldTypeName.Equals(typeof(long).AssemblyQualifiedName))
                {
                    sortFieldType = SortFieldType.INT64;
                }
                else if (fieldTypeName.Equals(typeof(float).AssemblyQualifiedName))
                {
                    sortFieldType = SortFieldType.SINGLE;
                }
                else if (fieldTypeName.Equals(typeof(double).AssemblyQualifiedName))
                {
                    sortFieldType = SortFieldType.DOUBLE;
                }
            }
            sorts.Add(new SortField(key,sortFieldType.Value,descending));
            sort.SetSort(sorts.ToArray());
            return this;
        }
        public void Clear() {
            query = "+" + this.Field(FieldKeys.PuckTypeChain, typeof(TModel).Name.Wrap()) + " ";
        }

        public QueryHelper<TModel> Format(Expression<Func<TModel, object>> exp) {
            query+= QueryHelper<TModel>.Format<TModel>(exp);
            return this;
        }

        public QueryHelper<TModel> Format(Expression<Func<TModel, object>> exp,params string[] values)
        {
            query += QueryHelper<TModel>.Format<TModel>(exp,values);
            return this;
        }
        
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp,string start,string end,bool inclusiveStart,bool inclusiveEnd)
        {
            string key=getName(exp.Body.ToString());
            string openTag = inclusiveStart ? "[" : "{";
            string closeTag = inclusiveEnd ? "]" : "}";
            query += string.Concat(key,":" , openTag ,start," TO ",end,closeTag," ");
            return this;
        }
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, string start, string end, bool inclusiveStart)
        {
            return this.Range(exp,start,end,inclusiveStart,true);
        }
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, string start, string end)
        {
            return this.Range(exp, start, end, true,true);
        }
        
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, int start, int end, bool inclusiveStart, bool inclusiveEnd)
        {
            return this.Range(exp,start.ToString(),end.ToString(),inclusiveStart,inclusiveEnd);
        }
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, int start, int end, bool inclusiveStart)
        {
            return this.Range(exp, start.ToString(), end.ToString(), inclusiveStart, true);
        }
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, int start, int end)
        {
            return this.Range(exp, start.ToString(), end.ToString(), true,true);
        }
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, long start, long end, bool inclusiveStart, bool inclusiveEnd)
        {
            return this.Range(exp, start.ToString(), end.ToString(), inclusiveStart, inclusiveEnd);
        }
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, long start, long end, bool inclusiveStart)
        {
            return this.Range(exp, start.ToString(), end.ToString(), inclusiveStart, true);
        }
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, long start, long end)
        {
            return this.Range(exp, start.ToString(), end.ToString(), true, true);
        }
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, float start, float end, bool inclusiveStart, bool inclusiveEnd)
        {
            return this.Range(exp, start.ToString(), end.ToString(), inclusiveStart, inclusiveEnd);
        }
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, float start, float end, bool inclusiveStart)
        {
            return this.Range(exp, start.ToString(), end.ToString(), inclusiveStart, true);
        }
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, float start, float end)
        {
            return this.Range(exp, start.ToString(), end.ToString(), true, true);
        }

        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, double start, double end, bool inclusiveStart, bool inclusiveEnd)
        {
            return this.Range(exp, start.ToString(), end.ToString(), inclusiveStart, inclusiveEnd);
        }
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, double start, double end, bool inclusiveStart)
        {
            return this.Range(exp, start.ToString(), end.ToString(), inclusiveStart, true);
        }
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, double start, double end)
        {
            return this.Range(exp, start.ToString(), end.ToString(), true, true);
        }
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, DateTime start, DateTime end, bool inclusiveStart, bool inclusiveEnd)
        {
            return this.Range(exp, start.ToString(dateFormat), end.ToString(dateFormat), inclusiveStart, inclusiveEnd);            
        }
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, DateTime start, DateTime end, bool inclusiveStart)
        {
            return this.Range(exp, start.ToString(dateFormat), end.ToString(dateFormat), inclusiveStart, true);
        }
        public QueryHelper<TModel> Range(Expression<Func<TModel, object>> exp, DateTime start, DateTime end)
        {
            return this.Range(exp, start.ToString(dateFormat), end.ToString(dateFormat), true,true);
        }
        //extended range
        public QueryHelper<TModel> GreaterThanEqualTo(Expression<Func<TModel, object>> exp, DateTime start)
        {
            return this.Range(exp, start.ToString(dateFormat), DateTime.MaxValue.ToString(dateFormat), true, true);
        }
        public QueryHelper<TModel> LessThanEqualTo(Expression<Func<TModel, object>> exp, DateTime end)
        {
            return this.Range(exp, DateTime.MinValue.ToString(dateFormat), end.ToString(dateFormat), true, true);
        }

        public QueryHelper<TModel> GreaterThanEqualTo(Expression<Func<TModel, object>> exp, int start)
        {
            return this.Range(exp, start.ToString(), int.MaxValue.ToString(), true, true);
        }
        public QueryHelper<TModel> LessThanEqualTo(Expression<Func<TModel, object>> exp, int end)
        {
            return this.Range(exp, int.MinValue.ToString(), end.ToString(), true, true);
        }

        public QueryHelper<TModel> GreaterThanEqualTo(Expression<Func<TModel, object>> exp, long start)
        {
            return this.Range(exp, start.ToString(), long.MaxValue.ToString(), true, true);
        }
        public QueryHelper<TModel> LessThanEqualTo(Expression<Func<TModel, object>> exp, long end)
        {
            return this.Range(exp, long.MinValue.ToString(), end.ToString(), true, true);
        }
        public QueryHelper<TModel> GreaterThanEqualTo(Expression<Func<TModel, object>> exp, float start)
        {
            return this.Range(exp, start.ToString(), float.MaxValue.ToString(), true, true);
        }
        public QueryHelper<TModel> LessThanEqualTo(Expression<Func<TModel, object>> exp, float end)
        {
            return this.Range(exp, float.MinValue.ToString(), end.ToString(), true, true);
        }
        public QueryHelper<TModel> GreaterThanEqualTo(Expression<Func<TModel, object>> exp, double start)
        {
            return this.Range(exp, start.ToString(), double.MaxValue.ToString(), true, true);
        }
        public QueryHelper<TModel> LessThanEqualTo(Expression<Func<TModel, object>> exp, double end)
        {
            return this.Range(exp, double.MinValue.ToString(), end.ToString(), true, true);
        }

        public QueryHelper<TModel> GreaterThan(Expression<Func<TModel, object>> exp, DateTime start)
        {
            return this.Range(exp, start.ToString(dateFormat), DateTime.MaxValue.ToString(dateFormat), false, true);
        }
        public QueryHelper<TModel> LessThan(Expression<Func<TModel, object>> exp, DateTime end)
        {
            return this.Range(exp, DateTime.MinValue.ToString(dateFormat), end.ToString(dateFormat), true, false);
        }

        public QueryHelper<TModel> GreaterThan(Expression<Func<TModel, object>> exp, int start)
        {
            return this.Range(exp, start.ToString(), int.MaxValue.ToString(), false, true);
        }
        public QueryHelper<TModel> LessThan(Expression<Func<TModel, object>> exp, int end)
        {
            return this.Range(exp, int.MinValue.ToString(), end.ToString(), true, false);
        }

        public QueryHelper<TModel> GreaterThan(Expression<Func<TModel, object>> exp, long start)
        {
            return this.Range(exp, start.ToString(), long.MaxValue.ToString(), false, true);
        }
        public QueryHelper<TModel> LessThan(Expression<Func<TModel, object>> exp, long end)
        {
            return this.Range(exp, long.MinValue.ToString(), end.ToString(), true, false);
        }
        public QueryHelper<TModel> GreaterThan(Expression<Func<TModel, object>> exp, float start)
        {
            return this.Range(exp, start.ToString(), float.MaxValue.ToString(), false, true);
        }
        public QueryHelper<TModel> LessThan(Expression<Func<TModel, object>> exp, float end)
        {
            return this.Range(exp, float.MinValue.ToString(), end.ToString(), true, false);
        }
        public QueryHelper<TModel> GreaterThan(Expression<Func<TModel, object>> exp, double start)
        {
            return this.Range(exp, start.ToString(), double.MaxValue.ToString(), false, true);
        }
        public QueryHelper<TModel> LessThan(Expression<Func<TModel, object>> exp, double end)
        {
            return this.Range(exp, double.MinValue.ToString(), end.ToString(), true, false);
        }
        
        private QueryHelper<TModel> GeoFilter(Expression<Func<TModel, object>> exp, double longitude, double latitude, double distDEG)
        {
            string name = getName(exp.Body.ToString());
            //name = name.IndexOf('.') > -1 ? name.Substring(0, name.LastIndexOf('.')) : name;
            SpatialOperation op = SpatialOperation.Intersects;
            //SpatialStrategy strat = new PointVectorStrategy(ctx, name);
            int maxLevels = 11;
            SpatialPrefixTree grid = new GeohashPrefixTree(ctx, maxLevels);
            var strat = new RecursivePrefixTreeStrategy(grid, name);

            var point = ctx.MakePoint(longitude, latitude);
            var shape = ctx.MakeCircle(point, distDEG);
            var args = new SpatialArgs(op, shape);
            filter = strat.MakeFilter(args);
            return this;
        }

        public QueryHelper<TModel> WithinMiles(Expression<Func<TModel, object>> exp, double longitude, double latitude, int miles)
        {
            var distDEG = DistanceUtils.Dist2Degrees(miles, DistanceUtils.EARTH_MEAN_RADIUS_MI);
            return GeoFilter(exp,longitude,latitude,distDEG);
        }

        public QueryHelper<TModel> WithinKilometers(Expression<Func<TModel, object>> exp, double longitude, double latitude, int kilometers)
        {
            var distDEG = DistanceUtils.Dist2Degrees(kilometers, DistanceUtils.EARTH_MEAN_RADIUS_KM);
            return GeoFilter(exp, longitude, latitude, distDEG);
        }

        public QueryHelper<TModel> AllFields(string value)
        {
            query += "+(";
            foreach (var k in PuckCache.TypeFields[typeof(TModel).AssemblyQualifiedName]){
                query += string.Concat(k.Key, ":", value, " ");
            }
            query+=") ";
            return this;
        }
        
        public QueryHelper<TModel> Field<T>(Expression<Func<T, object>> exp, object value)
        {
            string key = getName(exp.Body.ToString());
            return this.Field(key,value);
        }

        public QueryHelper<TModel> Field(string key, object value)
        {
            query += string.Concat(key, ":", value.ToString()," ");
            return this;
        }
        public QueryHelper<TModel> Field(Expression<Func<TModel, object>> exp, bool value)
        {
            string key = getName(exp.Body.ToString());
            query += string.Concat(key, ":", value.ToString(), " ");
            return this;
        }

        public QueryHelper<TModel> Field(Expression<Func<TModel, object>> exp, string value)
        {
            string key = getName(exp.Body.ToString());
            query += string.Concat(key , ":",  value," ");
            return this;
        }

        public QueryHelper<TModel> Field(Expression<Func<TModel, object>> exp, Guid value)
        {
            return this.Field(exp, value.ToString());
        }

        public QueryHelper<TModel> Field(Expression<Func<TModel, object>> exp, int ivalue)
        {
            return this.Field(exp,ivalue.ToString());
        }

        public QueryHelper<TModel> Field(Expression<Func<TModel, object>> exp, double dvalue)
        {
            return this.Field(exp, dvalue.ToString());
        }

        public QueryHelper<TModel> Field(Expression<Func<TModel, object>> exp, float fvalue)
        {
            return this.Field(exp, fvalue.ToString());
        }

        public QueryHelper<TModel> Field(Expression<Func<TModel, object>> exp, long lvalue)
        {
            return this.Field(exp, lvalue.ToString());
        }
        public QueryHelper<TModel> Path(string value)
        {
            this.Field(x => x.Path, value.Replace("/", @"\/"));
            return this;
        }

        //filters
        private void TrimAnd() {
            if (query.EndsWith("+"))
                query = query.TrimEnd('+');
        }
        public QueryHelper<TModel> AncestorsOf(BaseModel m) {
            return this.AncestorsOf(m.Path);
        }
        public QueryHelper<TModel> AncestorsOf(string path)
        {
            TrimAnd();
            string nodePath = path.ToLower();
            while (nodePath.Count(x => x == '/') > 1)
            {
                nodePath = nodePath.Substring(0, nodePath.LastIndexOf('/'));
                this.And()
                    //.Field(x => x.Path, nodePath);
                    .Path(nodePath);
            }
            return this;
        }
        public QueryHelper<TModel> SiblingsOf(BaseModel m)
        {
            return this.SiblingsOf(m.Path, m.Id.ToString());
        }
        public QueryHelper<TModel> SiblingsOf(string path, Guid id)
        {
            return this.SiblingsOf(path, id.ToString());
        }
        public QueryHelper<TModel> SiblingsOf(string path,string id)
        {
            TrimAnd();
            this
                    .And()
                    //.Field(x => x.Path, ApiHelper.DirOfPath(path.ToLower()).WildCardMulti())
                    .Path(ApiHelper.DirOfPath(path.ToLower()).WildCardMulti())
                    .Not()
                    //.Field(x => x.Path, ApiHelper.DirOfPath(path.ToLower()).WildCardMulti() + "/*")
                    .Path(ApiHelper.DirOfPath(path.ToLower()).WildCardMulti() + "/*")
                    .Not()
                    .Field(x => x.Id, id.Wrap());
            return this;
        }
        public QueryHelper<TModel> ChildrenOf(BaseModel m) {
            return this.ChildrenOf(m.Path);
        }
        public QueryHelper<TModel> ChildrenOf(string path)
        {
            TrimAnd();
            this
                    .And()
                    //.Field(x => x.Path, path.ToLower() + "/".WildCardMulti())
                    .Path(path.ToLower() + "/".WildCardMulti())
                    .Not()
                    //.Field(x => x.Path, path.ToLower() + "/".WildCardMulti() + "/*");
                    .Path(path.ToLower() + "/".WildCardMulti() + "/*");
            return this;
        }
        public QueryHelper<TModel> DescendantsOf(string path,bool must=true)
        {
            TrimAnd();
            if (must)
                this.And();
            this
                //.Field(x => x.Path, path.ToLower() + "/".WildCardMulti());
                .Path(path.ToLower() + "/".WildCardMulti());
            return this;
        }

        public QueryHelper<TModel> DescendantsOf(BaseModel m,bool must = true)
        {
            return this.DescendantsOf(m.Path,must:must);
        }

        public QueryHelper<TModel> CurrentRoot(BaseModel m = null)
        {
            TrimAnd();
            string currentPath=null;
            string currentRoot = null;
            if (m == null)
            {
                if (HttpContext.Current == null) return this;
                currentRoot = PathPrefix() + "/";
                this.And()
                    .Path(currentRoot.ToLower().WildCardMulti());
                return this;
            }
            else
            {
                currentPath = m.Path.TrimStart('/');
                if (currentPath.IndexOf("/") > -1)
                    currentRoot = currentPath.Substring(0, currentPath.IndexOf('/'));
                else currentRoot = currentPath;
                currentRoot = "/" + currentRoot + "/";
                this.And()
                    //.Field(x => x.Path, currentRoot.ToLower().WildCardMulti());
                    .Path(currentRoot.ToLower().WildCardMulti());
                return this;
            }
        }

        public QueryHelper<TModel> CurrentLanguage()
        {
            TrimAnd();
            var key = FieldKeys.Variant;
            var currentVariantFromHttpContext = HttpContext.Current?.Items["variant"];
            string v=null;
            if (currentVariantFromHttpContext == null && HttpContext.Current != null)
            {
                string absPath = HttpContext.Current.Request.GetUri().AbsolutePath.ToLower();
                string path = PathPrefix() + (absPath == "/" ? "" : absPath);
                v = new BaseController().GetVariant(path);
                HttpContext.Current.Items["variant"] = v;
            }
            else if (currentVariantFromHttpContext != null)
                v = currentVariantFromHttpContext as string;
                
            var variant = v??PuckCache.SystemVariant;
            query += string.Concat("+",key, ":", variant.ToLower(), " ");
            return this;
        }

        public QueryHelper<TModel> Level(int level)
        {
            TrimAnd();
            var includePath = string.Join("", Enumerable.Range(0, level).ToList().Select(x => "/*"));
            var excludePath = includePath + "/".WildCardMulti();
            var key = FieldKeys.Path;
            //query += string.Concat("+", key, ":", includePath, " -", key, ":", excludePath, " ");
            this.Must().Path(includePath).Not().Path(excludePath);
            return this;
        }

        public QueryHelper<TModel> ExplicitType<AType>()
        {
            TrimAnd();
            string key = FieldKeys.PuckType;
            query += string.Concat("+",key, ":", typeof(AType).Name.Wrap(), " ");
            return this;
        }

        public QueryHelper<TModel> ExplicitType()
        {
            TrimAnd();
            string key = FieldKeys.PuckType;
            query += string.Concat("+",key, ":", typeof(TModel).Name.Wrap(), " ");
            return this;
        }

        public QueryHelper<TModel> Implements<TI>()
        {
            var innerQ = this.New();
            var implementingTypes = ApiHelper.FindDerivedClasses(typeof(TI)).ToList();
            implementingTypes = implementingTypes.Where(x => typeof(BaseModel).IsAssignableFrom(x)).ToList();
            if (implementingTypes.Count == 0) return this;
            foreach (var type in implementingTypes)
            {
                innerQ.Field(x => x.Type, type.Name);
            }
            this.Must().Group(innerQ);
            return this;
        }

        public QueryHelper<TModel> Implements(params Type[] types)
        {
            var innerQ = this.New();
            var listOfListsOfTypes = new List<List<Type>>();
            
            foreach (var type in types)
            {
                var implementingTypes = ApiHelper.FindDerivedClasses(type).ToList();
                implementingTypes = implementingTypes.Where(x => typeof(BaseModel).IsAssignableFrom(x)).ToList();
                listOfListsOfTypes.Add(implementingTypes);
            }
            
            if (listOfListsOfTypes.Count == 0) return this;

            var intersectionOfImplementingTypes = listOfListsOfTypes
                .Skip(1)
                .Aggregate(
                    new HashSet<Type>(listOfListsOfTypes.First()),
                    (h, e) => { h.IntersectWith(e); return h; }
                );

            if (intersectionOfImplementingTypes.Count == 0) return this;

            foreach (var type in intersectionOfImplementingTypes)
            {
                innerQ.Field(x => x.Type, type.Name);
            }

            this.Must().Group(innerQ);
            return this;
        }

        public QueryHelper<TModel> Variant(string value,bool must=true)
        {
            TrimAnd();
            string key = FieldKeys.Variant;
            query += string.Concat(must?"+":"",key, ":", value.ToLower(), " ");
            return this;
        }

        public QueryHelper<TModel> Id(string value,bool must=true)
        {
            TrimAnd();
            string key = FieldKeys.ID;
            query += string.Concat(must?"+":"",key, ":", value, " ");
            return this;
        }

        public QueryHelper<TModel> Id(Guid value,bool must=true)
        {
            return this.Id(value.ToString(),must);
        }

        public QueryHelper<TModel> Directory(string value) {
            TrimAnd();
            string key = FieldKeys.Path;
            if (!value.EndsWith("/"))
                value += "/";
            //query += string.Concat("+",key,":",value.WildCardMulti()," -",key,":",value.WildCardMulti()+"/".WildCardMulti());
            this.Must().Path(value.WildCardMulti()).Not().Path(value.WildCardMulti()+"/".WildCardMulti());
            return this;
        }
        //end filters

        //logical operators
        public QueryHelper<TModel> Group(QueryHelper<TModel> q)
        {
            query += string.Concat("(", q.query, ") ");
            return this;
        }
        public QueryHelper<TModel> Must(QueryHelper<TModel> q = null) {
            return And(q);
        }
        public QueryHelper<TModel> And(QueryHelper<TModel> q=null)
        {
            TrimAnd();
            if (q == null)
            {
                query += "+";
            }
            else {
                query += string.Concat("+(",q.query,") ");
            }
            return this;
        }

        public QueryHelper<TModel> Or(QueryHelper<TModel> q = null)
        {
            if (q == null)
            {
                query += "OR ";
            }
            else
            {
                query += string.Concat("OR(", q.query, ") ");
            }
            return this;
        }

        public QueryHelper<TModel> Not(QueryHelper<TModel> q = null)
        {
            if (q == null)
            {
                query += "-";
            }
            else
            {
                query += string.Concat("-(", q.query, ") ");
            }
            return this;
        }

        //overrides
        public override string ToString()
        {
            return query;
        }

        //query executors
        public List<TModel> GetAll(int limit=500,int skip = 0)
        {
            var result = searcher.Query<TModel>(query,filter,sort,out totalHits,limit,skip).ToList();
            return result;
        }

        public List<TModel> GetAllNoCast(int limit=500,int skip = 0,Type typeOverride=null,bool fallBackToBaseModel=false)
        {
            var result = searcher.QueryNoCast<TModel>(query,filter,sort,out totalHits,limit,skip,typeOverride:typeOverride,fallBackToBaseModel:fallBackToBaseModel).ToList();
            return result;
        }

        public TModel Get()
        {
            var result = searcher.Query<TModel>(query,filter,sort,out totalHits,1,0).FirstOrDefault();
            return result;
        }

        public TModel GetNoCast()
        {
            var result = searcher.QueryNoCast<TModel>(query, filter, sort, out totalHits, 1, 0).FirstOrDefault();
            return result;
        }
    }
}
