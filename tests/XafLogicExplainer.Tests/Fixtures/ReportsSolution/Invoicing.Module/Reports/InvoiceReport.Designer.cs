namespace Invoicing.Module.Reports
{
    partial class InvoiceReport
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.TopMargin = new DevExpress.XtraReports.UI.TopMarginBand();
            this.BottomMargin = new DevExpress.XtraReports.UI.BottomMarginBand();
            this.Detail = new DevExpress.XtraReports.UI.DetailBand();
            this.GroupHeader1 = new DevExpress.XtraReports.UI.GroupHeaderBand();
            this.xrLabelCustomer = new DevExpress.XtraReports.UI.XRLabel();
            this.xrLabelNumber = new DevExpress.XtraReports.UI.XRLabel();
            this.xrLabelNet = new DevExpress.XtraReports.UI.XRLabel();
            this.collectionDataSource1 = new DevExpress.Persistent.Base.ReportsV2.CollectionDataSource(this.components);
            this.calculatedFieldNet = new DevExpress.XtraReports.UI.CalculatedField();
            this.parameterFrom = new DevExpress.XtraReports.Parameters.Parameter();
            ((System.ComponentModel.ISupportInitialize)(this.collectionDataSource1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            //
            // TopMargin
            //
            this.TopMargin.Name = "TopMargin";
            //
            // BottomMargin
            //
            this.BottomMargin.Name = "BottomMargin";
            //
            // Detail
            //
            this.Detail.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.xrLabelNumber,
            this.xrLabelNet});
            this.Detail.HeightF = 50F;
            this.Detail.Name = "Detail";
            //
            // GroupHeader1
            //
            this.GroupHeader1.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.xrLabelCustomer});
            this.GroupHeader1.GroupFields.AddRange(new DevExpress.XtraReports.UI.GroupField[] {
            new DevExpress.XtraReports.UI.GroupField("Customer.Name", DevExpress.XtraReports.UI.XRColumnSortOrder.Ascending)});
            this.GroupHeader1.HeightF = 30F;
            this.GroupHeader1.Name = "GroupHeader1";
            //
            // xrLabelCustomer
            //
            this.xrLabelCustomer.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[Customer.Name]")});
            this.xrLabelCustomer.LocationFloat = new DevExpress.Utils.LocationFloat(0F, 0F);
            this.xrLabelCustomer.Name = "xrLabelCustomer";
            this.xrLabelCustomer.SizeF = new System.Drawing.SizeF(300F, 23F);
            //
            // xrLabelNumber
            //
            this.xrLabelNumber.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[Number]")});
            this.xrLabelNumber.LocationFloat = new DevExpress.Utils.LocationFloat(0F, 0F);
            this.xrLabelNumber.Name = "xrLabelNumber";
            this.xrLabelNumber.SizeF = new System.Drawing.SizeF(150F, 23F);
            //
            // xrLabelNet
            //
            this.xrLabelNet.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "FormatString('{0:c}', [NetTotal])")});
            this.xrLabelNet.LocationFloat = new DevExpress.Utils.LocationFloat(150F, 0F);
            this.xrLabelNet.Name = "xrLabelNet";
            this.xrLabelNet.SizeF = new System.Drawing.SizeF(150F, 23F);
            //
            // collectionDataSource1
            //
            this.collectionDataSource1.Name = "collectionDataSource1";
            this.collectionDataSource1.ObjectTypeName = "Invoicing.Module.BusinessObjects.Invoice";
            //
            // calculatedFieldNet
            //
            this.calculatedFieldNet.Expression = "[Total] / 1.21";
            this.calculatedFieldNet.FieldType = DevExpress.XtraReports.UI.FieldType.Decimal;
            this.calculatedFieldNet.Name = "NetTotal";
            //
            // parameterFrom
            //
            this.parameterFrom.Description = "Invoices dated on or after";
            this.parameterFrom.Name = "From";
            this.parameterFrom.Type = typeof(System.DateTime);
            this.parameterFrom.Visible = false;
            //
            // InvoiceReport
            //
            this.Bands.AddRange(new DevExpress.XtraReports.UI.Band[] {
            this.TopMargin,
            this.BottomMargin,
            this.Detail,
            this.GroupHeader1});
            this.CalculatedFields.AddRange(new DevExpress.XtraReports.UI.CalculatedField[] {
            this.calculatedFieldNet});
            this.ComponentStorage.AddRange(new System.ComponentModel.IComponent[] {
            this.collectionDataSource1});
            this.DataSource = this.collectionDataSource1;
            this.FilterString = "[IsApproved] = True And [Date] >= ?From";
            this.Parameters.AddRange(new DevExpress.XtraReports.Parameters.Parameter[] {
            this.parameterFrom});
            this.Version = "25.2";
            ((System.ComponentModel.ISupportInitialize)(this.collectionDataSource1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this)).EndInit();

        }

        #endregion

        private DevExpress.XtraReports.UI.TopMarginBand TopMargin;
        private DevExpress.XtraReports.UI.BottomMarginBand BottomMargin;
        private DevExpress.XtraReports.UI.DetailBand Detail;
        private DevExpress.XtraReports.UI.GroupHeaderBand GroupHeader1;
        private DevExpress.XtraReports.UI.XRLabel xrLabelCustomer;
        private DevExpress.XtraReports.UI.XRLabel xrLabelNumber;
        private DevExpress.XtraReports.UI.XRLabel xrLabelNet;
        private DevExpress.Persistent.Base.ReportsV2.CollectionDataSource collectionDataSource1;
        private DevExpress.XtraReports.UI.CalculatedField calculatedFieldNet;
        private DevExpress.XtraReports.Parameters.Parameter parameterFrom;
    }
}
