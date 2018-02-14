using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using CnBlogSubscribeTool.Config;
using HtmlAgilityPack;
using Newtonsoft.Json;
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
        private static Logger _sendLogger;
        private static MailConfig _mailConfig;
        private static string _baseDir;
        private static RetryPolicy _retryTwoTimesPolicy;
        private static string _tmpFilePath;
        
        private static DateTime _recordTime;
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

            //初始化重试器
            _retryTwoTimesPolicy =
                Policy
                    .Handle<Exception>()
                    .Retry(3, (ex, count) =>
                    {
                        _logger.Error("Excuted Failed! Retry {0}", count);
                        _logger.Error("Exeption from {0}", ex.GetType().Name);
                    });

            //获取应用程序所在目录
            Type type = (new Program()).GetType();
            _baseDir = Path.GetDirectoryName(type.Assembly.Location);

            //检查工作目录
            if (!Directory.Exists(Path.Combine(_baseDir, "Blogs")))
            {
                Directory.CreateDirectory(Path.Combine(_baseDir, "Blogs"));
            }

            //初始化记录时间
            _recordTime=new DateTime(DateTime.Now.Year, DateTime.Now.Month, 13, 9,0,0);


            //初始化日志
            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(_baseDir, "Config", "NLog.Config"));
            _logger = LogManager.GetLogger("CnBlogSubscribeTool");
            _sendLogger = LogManager.GetLogger("MailSend");

            //初始化邮件配置
            _mailConfig =
                JsonConvert.DeserializeObject<MailConfig>(
                    File.ReadAllText(Path.Combine(_baseDir, "Config", "Mail.json")));

            //加载最后一次成功获取数据缓存

            _tmpFilePath = Path.Combine(_baseDir, "Blogs", "cnblogs.tmp");
            if (File.Exists(_tmpFilePath))
            {
                try
                {
                    var data = File.ReadAllText(_tmpFilePath);
                    var res = JsonConvert.DeserializeObject<List<Blog>>(data);
                    if (res != null)
                    {
                        PreviousBlogs.AddRange(res);
                    }
                }
                catch (Exception e)
                {
                    _logger.Error("缓存数据加载失败，本次将弃用！详情:" + e.Message);
                    File.Delete(_tmpFilePath);
                }
            }
        }

        static void WorkStart()
        {
            try
            {
                while (true)
                {
                    
                    _retryTwoTimesPolicy.Execute(Work);

                    //每五分钟执行一次
                    Thread.Sleep(300000);
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

                string html = HttpUtil.GetString(BlogDataUrl);

                List<Blog> blogs = new List<Blog>();

                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

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

                string blogFileName= $"cnblogs-{DateTime.Now:yyyy-MM-dd}.txt";
                string blogFilePath = Path.Combine(_baseDir, "Blogs", blogFileName);
                FileStream fs=new FileStream(blogFilePath, FileMode.Append,FileAccess.Write);

                StreamWriter sw=new StreamWriter(fs,Encoding.UTF8);
                //去重
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

                //持久化本次抓取数据到文本 以便于异常退出恢复之后不出现重复数据
                File.WriteAllText(_tmpFilePath, JsonConvert.SerializeObject(blogs));

                Sw.Stop();

                //统计信息

                _logger.Info($"Get data success,Time:{Sw.ElapsedMilliseconds}ms,Data Count:{blogs.Count},Repeat:{repeatCount},Effective:{blogs.Count - repeatCount}");

                //发送邮件
                if ((DateTime.Now - _recordTime).TotalHours >= 24)
                {
                    _sendLogger.Info($"准备发送邮件，记录时间:{_recordTime:yyyy-MM-dd HH:mm:ss}");
                    SendMail();
                    _recordTime= new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 9, 0, 0);
                    _sendLogger.Info($"记录时间已更新:{_recordTime:yyyy-MM-dd HH:mm:ss}");
                }
                
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

        /// <summary>
        /// 发送邮件
        /// </summary>
        static void SendMail()
        {
            string blogFileName = $"cnblogs-{_recordTime:yyyy-MM-dd}.txt";
            string blogFilePath = Path.Combine(_baseDir, "Blogs", blogFileName);

            if (!File.Exists(blogFilePath))
            {
                _sendLogger.Error("未发现文件记录，无法发送邮件，所需文件名："+blogFileName);
                return;
            }
            //邮件正文
            string mailContent = "";
            FileStream mailFs = new FileStream(blogFilePath, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(mailFs, Encoding.UTF8);
            while (!sr.EndOfStream)
            {
                mailContent += sr.ReadLine() + "<br/>";
            }
            sr.Close();
            mailFs.Close();

            //附件内容
            string blogFileContent = File.ReadAllText(blogFilePath);

            //发送邮件
            MailUtil.SendMail(_mailConfig,_mailConfig.ReceiveList, "CnBlogSubscribeTool",
                $"博客园首页文章聚合-{_recordTime:yyyy-MM-dd}", mailContent, Encoding.UTF8.GetBytes(blogFileContent),
                blogFileName);

            _sendLogger.Info($"{blogFileName},文件已发送");
        }
    }
}
