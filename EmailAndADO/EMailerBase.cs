using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Mail;

namespace EmailAndADO
{
    //Clase para enviar todos los correos electronicos
    public class EMailerBase
    {
        MailMessage email;
        SmtpClient emailClient;

        private void SendEmail()
        {
            try
            {
                emailClient.Send(email);
            }
            catch (Exception e)
            {
                string innerEx = "";

                if (e.InnerException != null)
                { innerEx = e.InnerException.Message; }

                new LogBO().WriteExceptionHandledLog("Error al enviar correos: " + e.Message + innerEx);
            }

            try { System.Threading.Thread.CurrentThread.Abort(); }
            catch { }
        }

        public bool SendEmail(string Subject, string MessageBody, List<string> DestinedEmailAddresses)
        {
            try
            {
                email = new MailMessage();

                List<string> sentMails = new List<string>();

                foreach (string destinedEmail in DestinedEmailAddresses)
                {
                    //Verifica que sea solo 1 vez cada correo electronico
                    if (!sentMails.Contains(destinedEmail))
                    {
                        sentMails.Add(destinedEmail);

                        try
                        { email.To.Add(destinedEmail); }
                        catch (Exception)
                        { 
                            new LogBO().WriteExceptionHandledLog(string.Format("Error al intentar enviar un correo a: '{0}'", destinedEmail));
                            sentMails.Remove(destinedEmail);
                        }
                        
                    }
                }

                bool sentEmail = false;

                VEmailConfig configE = new EmailConfigBO().Select(EmailConfigType.ThunderLinkEmail);
                if ((configE != null)
                    && (configE.IdTEmailConfig > 0))
                {
                    email.From = new MailAddress(configE.EmailComplete);
                    email.Subject = Subject + "- Please do not reply to this e-mail -";
                    
                    StringBuilder sbBody = new StringBuilder(MessageBody);
                    sbBody.AppendLine(configE.SenderSignature);

                    email.Body = sbBody.ToString();
                    email.IsBodyHtml = false;

                    emailClient = new SmtpClient();
                    emailClient.Host = configE.EmailServer;
                    emailClient.Port = configE.EmailServerPort;
                    emailClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                    emailClient.UseDefaultCredentials = false;
                    emailClient.Credentials = new System.Net.NetworkCredential(configE.SenderUsername, configE.SenderPassword);
                    emailClient.EnableSsl = configE.IsSSL;

                    System.Threading.Thread sendMails = new System.Threading.Thread(new System.Threading.ThreadStart(SendEmail));
                    sendMails.Start();

                    sentEmail = true;
                }

                return sentEmail;
            }
            catch (Exception e)
            {
                string innerEx = "";
                if (e.InnerException != null)
                { innerEx = e.InnerException.Message; }

                new LogBO().WriteExceptionHandledLog(string.Format("Error al enviar correos a: {0}, MESSAGE: {1}, EX: {2} ",email.To, e.Message, innerEx));

                return false;
            }
        }

    }
}
