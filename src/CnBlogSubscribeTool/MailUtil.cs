using System.Collections.Generic;
using CnBlogSubscribeTool.Config;
using MailKit.Net.Smtp;
using MimeKit;

namespace CnBlogSubscribeTool
{
    /// <summary>
    /// send email
    /// </summary>
    public class MailUtil
    {
        private static bool SendMail(MimeMessage mailMessage,MailConfig config)
        {
            try
            {
                var smtpClient = new SmtpClient();
                smtpClient.Timeout = 10 * 1000;   //设置超时时间
                smtpClient.Connect(config.Host, config.Port, MailKit.Security.SecureSocketOptions.None);//连接到远程smtp服务器
                smtpClient.Authenticate(config.Address, config.Password);
                smtpClient.Send(mailMessage);//发送邮件
                smtpClient.Disconnect(true);
                return true;

            }
            catch
            {
                throw;
            }

        }

        /// <summary>
        ///发送邮件
        /// </summary>
        /// <param name="config">配置</param>
        /// <param name="receives">接收人</param>
        /// <param name="sender">发送人</param>
        /// <param name="subject">标题</param>
        /// <param name="body">内容</param>
        /// <param name="attachments">附件</param>
        /// <param name="fileName">附件名</param>
        /// <returns></returns>
        public static bool SendMail(MailConfig config,List<string> receives, string sender, string subject, string body, byte[] attachments = null,string fileName="")
        {
            var fromMailAddress = new MailboxAddress(config.Name, config.Address);
            
            var mailMessage = new MimeMessage();
            mailMessage.From.Add(fromMailAddress);
            
            foreach (var add in receives)
            {
                var toMailAddress = new MailboxAddress(add);
                mailMessage.To.Add(toMailAddress);
            }
            if (!string.IsNullOrEmpty(sender))
            {
                var replyTo = new MailboxAddress(config.Name, sender);
                mailMessage.ReplyTo.Add(replyTo);
            }
            var bodyBuilder = new BodyBuilder() { HtmlBody = body };

            //附件
            if (attachments != null)
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = "未命名文件.txt";
                }
                var attachment = bodyBuilder.Attachments.Add(fileName, attachments);

                //解决中文文件名乱码
                var charset = "GB18030";
                attachment.ContentType.Parameters.Clear();
                attachment.ContentDisposition.Parameters.Clear();
                attachment.ContentType.Parameters.Add(charset, "name", fileName);
                attachment.ContentDisposition.Parameters.Add(charset, "filename", fileName);

                //解决文件名不能超过41字符
                foreach (var param in attachment.ContentDisposition.Parameters)
                    param.EncodingMethod = ParameterEncodingMethod.Rfc2047;
                foreach (var param in attachment.ContentType.Parameters)
                    param.EncodingMethod = ParameterEncodingMethod.Rfc2047;
            }

            mailMessage.Body = bodyBuilder.ToMessageBody();
            mailMessage.Subject = subject;

            return SendMail(mailMessage, config);

        }
    }
}