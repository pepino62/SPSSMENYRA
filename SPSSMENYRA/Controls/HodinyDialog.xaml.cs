using JisResources;
using JisResources.Sources;
using System.Globalization;
using System.Windows;

namespace SPSSMENYRA
{
    /// <summary>
    /// rucni oprava hodin pracovnika (VFP editace sloupce hod. v gridu - f00n3)
    /// </summary>
    public partial class HodinyDialog : JisWindow
    {
        decimal? hodiny;

        public HodinyDialog()
        {
            Name = "SmenyHodiny";
            InitializeComponent();
            jisButtonMaxVisible = false;
            jisButtonMinVisible = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        /// <summary>
        /// vraci zadane hodiny, null = zpet
        /// </summary>
        public decimal? ShowDialog(Window owner, string popis, decimal hod)
        {
            Owner = owner;
            txtPopis.Text = popis;
            txtHodiny.Text = hod.ToString( "0.0", CultureInfo.InvariantCulture );
            Loaded += (s, e) => { txtHodiny.Focus(); txtHodiny.SelectAll(); };
            ShowDialog();
            return hodiny;
        }

        private void BtnUlozit_Click(object sender, RoutedEventArgs e)
        {
            string text = txtHodiny.Text.Trim().Replace( ',', '.' );
            if (!decimal.TryParse( string.IsNullOrEmpty( text ) ? "0" : text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal h ))
            {
                Msg.Ok( "Zadejte počet hodin, např. 5.5" );
                return;
            }
            hodiny = h;
            Close();
        }

        private void BtnZpet_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
