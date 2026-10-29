using JisResources;
using JisResources.Sources;
using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SPSSMENYRA
{
    /// <summary>
    /// cerpani dovolene pracovnika v roce - zadani rozsahu od-do, prehled dnu, pul dne / cely den, smazani (VFP SPORT11)
    /// </summary>
    public partial class Dovolena : JisWindow
    {
        readonly string ft301;
        readonly int rok;

        public Dovolena(string ft301, string jmeno, int rok)
        {
            this.ft301 = ft301;
            this.rok = rok;
            Name = "SmenyDovolena";

            InitializeComponent();
            jisButtonMaxVisible = false;
            jisButtonMinVisible = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            txtJmeno.Text = jmeno;
            lblPlan.Content = $"počet dnů dovolené {rok} :";
            DateTime den = DateTime.Today.Year == rok ? DateTime.Today : new DateTime( rok, 1, 1 );
            dtpOd.SelectedDate = den;
            dtpDo.SelectedDate = den;
            Nacist();
        }

        public void ShowDialog(Window owner)
        {
            Owner = owner;
            ShowDialog();
        }

        /// <summary>
        /// dny cerpani v roce a soucty (VFP SPORT11_TMP)
        /// </summary>
        void Nacist()
        {
            SpravaDat.Select( "_d1dovolena", $@"
                SELECT d1.f101, d1.f103, d1.f104,
                    ELT(DAYOFWEEK(d1.f104), 'Ne', 'Po', 'Út', 'St', 'Čt', 'Pá', 'So') AS fdayc
                FROM {Smena.D1} d1
                WHERE d1.ft301 = @ft301 AND d1.f102 = '2' AND YEAR(d1.f104) = @rok
                ORDER BY d1.f104", new { ft301, rok }, hide: true );
            DataTable dt = Goa.Table( "_d1dovolena" );
            dgCerpani.ItemsSource = dt.DefaultView;

            txtPlan.Text = $"{PracovnikSluzba.Dovolena( ft301, rok, PracovnikSluzba.Plan ):0.0}";
            txtCerpano.Text = $"{dt.AsEnumerable().Sum( r => Convert.ToDecimal( r[ "f103" ] ) ):0.0}";
        }

        private void Rozsah_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (txtDnu == null)
                return;
            int dnu = dtpOd.SelectedDate is DateTime od && dtpDo.SelectedDate is DateTime @do ? (@do.Date - od.Date).Days + 1 : 0;
            txtDnu.Text = dnu > 0 ? $"{dnu}" : "";     // VFP SPORT11_DOVDNU
        }

        private void BtnUlozit_Click(object sender, RoutedEventArgs e)
        {
            Proved( () =>
            {
                if (!(dtpOd.SelectedDate is DateTime od) || !(dtpDo.SelectedDate is DateTime @do))
                    throw new JisException( "Zadejte rozsah dovolené od - do" );
                if (od.Year != rok || @do.Year != rok)
                    throw new JisException( $"Rozsah dovolené musí být v roce {rok}" );
                int pridano = PracovnikSluzba.PridatCerpani( ft301, od, @do );
                if (pridano == 0)
                    Msg.Ok( "Všechny dny tohoto rozsahu již jsou zapsány" );
            } );
        }

        private void BtnPulDne_Click(object sender, RoutedEventArgs e)
        {
            Proved( () =>
            {
                var vybrane = dgCerpani.SelectedItems.OfType<DataRowView>().ToList();
                if (vybrane.Count == 0)
                    throw new JisException( "Vyberte v seznamu den dovolené" );
                using (var tr = SpravaDat.Transakce())
                {
                    foreach (DataRowView r in vybrane)
                        PracovnikSluzba.ZmenitCerpani( $"{r[ "f101" ]}".Trim(), Convert.ToDecimal( r[ "f103" ] ) == 1 ? 0.5m : 1m );
                    tr.Commit();
                }
            } );
        }

        private void BtnSmazat_Click(object sender, RoutedEventArgs e)
        {
            Proved( () =>
            {
                var vybrane = dgCerpani.SelectedItems.OfType<DataRowView>().ToList();
                if (vybrane.Count == 0)
                    throw new JisException( "Vyberte v seznamu den dovolené" );
                if (Msg.YesNo( $"Smazat vybrané dny dovolené ( {vybrane.Count} ) ?" ))
                    PracovnikSluzba.SmazatCerpani( vybrane.Select( r => $"{r[ "f101" ]}".Trim() ) );
            } );
        }

        private void BtnZavrit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        void Proved(Action akce)
        {
            try
            {
                using (new WaitCursor())
                    akce();
            }
            catch (JisException e1)
            {
                if (!string.IsNullOrEmpty( e1.Text ))
                    Msg.Ok( e1.Text );
            }
            catch (Exception ex)
            {
                Msg.Err( ex );
            }
            Nacist();
        }
    }
}
