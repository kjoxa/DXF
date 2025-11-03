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
            prodBox = new CheckBox();
            SuspendLayout();
            // 
            // btnOpenJson
            // 
            resources.ApplyResources(btnOpenJson, "btnOpenJson");
            btnOpenJson.Name = "btnOpenJson";
            btnOpenJson.UseVisualStyleBackColor = true;
            btnOpenJson.Click += btnOpenJson_Click;
            // 
            // prodBox
            // 
            resources.ApplyResources(prodBox, "prodBox");
            prodBox.Name = "prodBox";
            prodBox.UseVisualStyleBackColor = true;
            // 
            // MainFrm
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(prodBox);
            Controls.Add(btnOpenJson);
            Name = "MainFrm";
            Load += MainFrm_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnOpenJson;
        private CheckBox prodBox;
    }
}
