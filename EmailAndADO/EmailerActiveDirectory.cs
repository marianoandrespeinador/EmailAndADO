using System.Collections.Generic;
using System.Text;

namespace EmailAndADO
{
    class EmailerActiveDirectory : EMailerBase
    {
        public bool SendEmailUserChangePassword(List<string> LstEmails, string UserChanged)
        {
            StringBuilder sbMessage = new StringBuilder();
            sbMessage.AppendLine("Active Directory: Password de Usuario ha sido cambiado.");
            sbMessage.AppendLine();
            sbMessage.AppendLine("Username: " + UserChanged);
            sbMessage.AppendLine("Cambiado por: " + GlobalVariables.UserCompleteName);

            return base.SendEmail("Active Directory: Cambio de Password.", sbMessage.ToString(), LstEmails);
        }

        public bool SendEmailUserInsert(List<string> LstEmails, string UserInserted)
        {
            StringBuilder sbMessage = new StringBuilder();
            sbMessage.AppendLine("Active Directory: Usuario ha sido Insertado.");
            sbMessage.AppendLine();
            sbMessage.AppendLine("Username: " + UserInserted);
            sbMessage.AppendLine("Agregado por: " + GlobalVariables.UserCompleteName);

            return base.SendEmail("Active Directory: Ingreso de Usuario.", sbMessage.ToString(), LstEmails);
        }

        public bool SendEmailUserChangeStatus(List<string> LstEmails, string UserChanged, string WordStatus)
        {
            StringBuilder sbMessage = new StringBuilder();
            sbMessage.AppendLine("Active Directory: Usuario ha sido " + WordStatus + ".");
            sbMessage.AppendLine();
            sbMessage.AppendLine("Username: " + UserChanged);
            sbMessage.AppendLine("Cambiado por: " + GlobalVariables.UserCompleteName);

            return base.SendEmail("Active Directory: Usuario " + WordStatus + ".", sbMessage.ToString(), LstEmails);
        }

        public bool SendEmailUserRehire(List<string> LstEmails, VUser UserRehired)
        {
            StringBuilder sbMessage = new StringBuilder();
            sbMessage.AppendLine("Active Directory: Usuario ha sido recontratado, se debe de Ingresar en AD.");
            sbMessage.AppendLine();
            sbMessage.AppendLine("Username: " + UserRehired.Username);
            sbMessage.AppendLine("Nombre completo: " + UserRehired.FullName);
            sbMessage.AppendLine("Código Empleado: " + UserRehired.EECode);
            sbMessage.AppendLine("Recontratado por: " + GlobalVariables.UserCompleteName);

            return base.SendEmail("Active Directory: Usuario Recontratado.", sbMessage.ToString(), LstEmails);
        }
    }
}
