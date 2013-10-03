using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.SqlAnywhere
{
    internal sealed class SqlAnywhereDatabaseProviderEditor : ProviderEditorBase
    {
        private ValidatingTextBox txtConnectionString;

        protected override void CreateChildControls()
        {
            this.txtConnectionString = new ValidatingTextBox
            {
                Width = 300,
                Required = true,
                TextMode = TextBoxMode.MultiLine,
                Rows = 5
            };

            this.Controls.Add(
                new FormFieldGroup(
                    "Connection String",
                    "The connection string to the SqlAnywhere database. The standard format for this is:<br /><br />"
                    + "<em>UserID=DBA; Password=sql; DatabaseName=myDatabaseName; Host=myServerName</em>",
                    false,
                    new StandardFormField(string.Empty, this.txtConnectionString)
                )
            );

            base.CreateChildControls();
        }
        public override void BindToForm(ProviderBase extension)
        {
            this.EnsureChildControls();

            var sqlAnywhere = (SqlAnywhereDatabaseProvider)extension;
            this.txtConnectionString.Text = sqlAnywhere.ConnectionString;
        }

        public override ProviderBase CreateFromForm()
        {
            this.EnsureChildControls();

            return new SqlAnywhereDatabaseProvider
            {
                ConnectionString = this.txtConnectionString.Text
            };
        }
    }
}
