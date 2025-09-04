namespace Klimor.WebApi.DXF
{
    partial class MainFrm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainFrm));
            btnOpenJson = new Button();
            SuspendLayout();
            // 
            // btnOpenJson
            // 
            resources.ApplyResources(btnOpenJson, "btnOpenJson");
            btnOpenJson.Name = "btnOpenJson";
            btnOpenJson.UseVisualStyleBackColor = true;
            btnOpenJson.Click += btnOpenJson_Click;
            // 
            // MainFrm
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(btnOpenJson);
            Name = "MainFrm";
            Load += MainFrm_Load;
            ResumeLayout(false);
        }

        #endregion

        private Button btnOpenJson;
    }
}
