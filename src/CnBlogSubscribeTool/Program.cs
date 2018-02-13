using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using HtmlAgilityPack;
using NLog;
using NLog.Config;
using Polly;
using Polly.Retry;

namespace CnBlogSubscribeTool
{
    class Program
    {
        private static string BlogDataUrl= "https://www.cnblogs.com/";
        private static readonly Stopwatch Sw = new Stopwatch();
        private static readonly List<Blog> PreviousBlogs = new List<Blog>();
        private static Logger _logger;
        private static RetryPolicy _retryTwoTimesPolicy;
        static void Main(string[] args)
        {
            Init();

            //work thread
            new Thread(new ThreadStart(WorkStart)).Start();

            Console.Title = "Cnblogs Article Archives Tool";
            Console.WriteLine("Service Working...");
            Console.ReadKey();
        }

        static void Init()
        {
            _retryTwoTimesPolicy =
                Policy
                    .Handle<Exception>()
                    .Retry(3, (ex, count) =>
                    {
                        _logger.Error("Excuted Failed! Retry {0}", count);
                        _logger.Error("Exeption from {0}", ex.GetType().Name);
                    });

            dynamic type = (new Program()).GetType();
            string currentDirectory = Path.GetDirectoryName(type.Assembly.Location);

            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(currentDirectory, "NLog.Config"));
            _logger = LogManager.GetLogger("CnBlogSubscribeTool");
        }

        static void WorkStart()
        {
            try
            {
                while (true)
                {
                    
                    _retryTwoTimesPolicy.Execute(Work);

                    Thread.Sleep(60000);
                }
                
            }
            catch (Exception e)
            {
                _logger.Error($"Excuted Failed,Message: ({e.Message})");

            }
        }

        /// <summary>
        /// 抓取
        /// </summary>
        static void Work()
        {
            try
            {
                Sw.Reset();
                Sw.Start();

                //重复数量统计
                int repeatCount = 0;

                string res = HttpUtil.GetString(BlogDataUrl);

                List<Blog> blogs = new List<Blog>();

                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(res);

                //获取所有文章数据项
                var itemBodys = doc.DocumentNode.SelectNodes("//div[@class='post_item_body']");

                foreach (var itemBody in itemBodys)
                {
                    //标题元素
                    var titleElem = itemBody.SelectSingleNode("h3/a");
                    //获取标题
                    var title = titleElem?.InnerText;
                    //获取url
                    var url = titleElem?.Attributes["href"]?.Value;

                    //摘要元素
                    var summaryElem = itemBody.SelectSingleNode("p[@class='post_item_summary']");
                    //获取摘要
                    var summary = summaryElem?.InnerText.Replace("\r\n", "").Trim();

                    //数据项底部元素
                    var footElem = itemBody.SelectSingleNode("div[@class='post_item_foot']");
                    //获取作者
                    var author = footElem?.SelectSingleNode("a")?.InnerText;
                    //获取文章发布时间
                    var publishTime = Regex.Match(footElem?.InnerText, "\\d+-\\d+-\\d+ \\d+:\\d+").Value;

                    //组装博客对象
                    Blog blog = new Blog()
                    {
                        Title = title,
                        Url = url,
                        Summary = summary,
                        Author = author,
                        PublishTime = DateTime.Parse(publishTime)
                    };
                    blogs.Add(blog);


                    /*Console.WriteLine($"标题：{title}");
                    Console.WriteLine($"网址：{url}");
                    Console.WriteLine($"摘要：{summary}");
                    Console.WriteLine($"作者：{author}");
                    Console.WriteLine($"发布时间：{publishTime}");
                    Console.WriteLine("--------------华丽的分割线---------------");*/
                }

                //去重
                FileStream fs=new FileStream($"cnblogs{DateTime.Now:yyyy-MM-dd}.txt",FileMode.Append,FileAccess.Write);
                StreamWriter sw=new StreamWriter(fs,Encoding.UTF8);
                foreach (var blog in blogs)
                {
                    if (PreviousBlogs.Any(b => b.Url == blog.Url))
                    {
                        repeatCount++;
                    }
                    else
                    {
                        sw.WriteLine($"标题：{blog.Title}");
                        sw.WriteLine($"网址：{blog.Url}");
                        sw.WriteLine($"摘要：{blog.Summary}");
                        sw.WriteLine($"作者：{blog.Author}");
                        sw.WriteLine($"发布时间：{blog.PublishTime:yyyy-MM-dd HH:mm}");
                        sw.WriteLine("--------------华丽的分割线---------------");
                    }
                    
                }
                sw.Close();
                fs.Close();

                //清除上一次抓取数据记录
                PreviousBlogs.Clear();
                //加入本次抓取记录
                PreviousBlogs.AddRange(blogs);

                Sw.Stop();

                //统计信息

                _logger.Info($"Get data success,Time:{Sw.ElapsedMilliseconds}ms,Data Count:{blogs.Count},Repeat:{repeatCount},Effective:{blogs.Count - repeatCount}");
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                Sw.Stop();
            }
        }
    }
}
