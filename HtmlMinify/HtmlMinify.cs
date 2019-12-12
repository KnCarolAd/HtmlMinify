using Microsoft.Ajax.Utilities;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.UI;

namespace HtmlMinify
{
    public class HtmlMinify : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.RequestContext.HttpContext.Response.Output is HttpWriter)
            {
                filterContext.HttpContext.Items["outputWriter"] = filterContext.RequestContext.HttpContext.Response.Output;
                filterContext.RequestContext.HttpContext.Response.Output = new HtmlTextWriter(new StringWriter());
            }
        }

        public override void OnResultExecuted(ResultExecutedContext filterContext)
        {
            if (!filterContext.HttpContext.Items.Contains("outputWriter") || !(filterContext.RequestContext.HttpContext.Response.Output is HtmlTextWriter))
                return;

            HtmlTextWriter writer = (HtmlTextWriter)filterContext.RequestContext.HttpContext.Response.Output;
            string response = writer.InnerWriter.ToString();

            //JavaScript
            string jspattern = "<script>([\\S\\s]*?)<\\/script>";
            if (Regex.IsMatch(response, jspattern))
            {
                var mc = Regex.Matches(response, jspattern).Cast<Match>().Reverse();
                foreach (Match m in mc)
                {
                    Group group = m.Groups[1];
                    string minify = JsMinify(group.Value);
                    if (string.IsNullOrWhiteSpace(group.Value) || string.IsNullOrWhiteSpace(minify))
                        response = response.Remove(m.Index, m.Length);
                    else
                        response = ReplaceAtIndex(response, minify, group.Index, group.Length);
                }
            }

            //CSS
            string csspattern = "<style>([\\S\\s]*?)<\\/style>";
            if (Regex.IsMatch(response, csspattern))
            {
                var mc = Regex.Matches(response, csspattern).Cast<Match>().Reverse();
                foreach (Match m in mc)
                {
                    Group group = m.Groups[1];
                    string minify = CssMinify(group.Value);
                    if (string.IsNullOrWhiteSpace(group.Value) || string.IsNullOrWhiteSpace(minify))
                        response = response.Remove(m.Index, m.Length);
                    else
                        response = ReplaceAtIndex(response, minify, group.Index, group.Length);
                }

                //CSS移動到body的最前面
                if (Regex.IsMatch(response, csspattern))
                {
                    mc = Regex.Matches(response, csspattern).Cast<Match>().Reverse();
                    foreach (Match m in mc)
                    {
                        response = response.Remove(m.Index, m.Length);
                    }
                    string bodypattern = "<body>";
                    int start = 0;
                    if (Regex.IsMatch(response, bodypattern))
                    {
                        Match m = Regex.Match(response, bodypattern);
                        start = m.Index + m.Length;
                    }
                    foreach (Match m in mc)
                    {
                        response = response.Insert(start, m.Value);
                    }
                }
            }

            //html
            //string pattern = "<[A-Za-z\\/]{1}[^><]*>([\\s]+)<[A-Za-z\\/]{1}[^><]*>";
            //while (Regex.IsMatch(response, pattern))
            //{
            //    MatchCollection mc = Regex.Matches(response, pattern, RegexOptions.Compiled);
            //    for (int i = mc.Count - 1; i >= 0; i--)
            //    {
            //        Group group = mc[i].Groups[1];
            //        response = response.Remove(group.Index, group.Length);
            //    }
            //}

            ((HttpWriter)filterContext.HttpContext.Items["outputWriter"]).Write(response);
        }

        private string JsMinify(string sourceJs)
        {
            if (!BundleTable.EnableOptimizations)
            {
                return sourceJs;
            }
            if (string.IsNullOrWhiteSpace(sourceJs))
            {
                return string.Empty;
            }
            Minifier minifier = new Minifier();
            string minifiedJs = minifier.MinifyJavaScript(sourceJs, new CodeSettings
            {
                EvalTreatment = EvalTreatment.MakeImmediateSafe,
                PreserveImportantComments = false
            });
            return minifiedJs;
        }

        private string CssMinify(string sourceCss)
        {
            if (!BundleTable.EnableOptimizations)
            {
                return sourceCss;
            }
            if (string.IsNullOrWhiteSpace(sourceCss))
            {
                return string.Empty;
            }
            Minifier minifier = new Minifier();
            string minifiedCss = minifier.MinifyStyleSheet(sourceCss, new CssSettings
            {
                CommentMode = CssComment.None
            });
            return minifiedCss;
        }

        private string ReplaceAtIndex(string text, string replaceText, int index, int length)
        {
            var firstPart = text.Substring(0, index);
            var secondPart = text.Substring(index + length);
            return firstPart + replaceText + secondPart;
        }
    }
}
