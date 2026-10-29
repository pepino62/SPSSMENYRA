using JisResources;
using JisResources.Sources;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SPSSMENYRA
{
    /// <summary>
    /// karta pracovnika - novy / oprava udaju, planovana dovolena na rok (VFP SPORT1)
    /// </summary>
    public partial class KartaPracovnika : JisWindow
    {
        readonly int rok;
        bool ulozeno;

        public PracovnikModel Pracovnik { get; private set; }
        public List<Polozka> Typy { get; } = Ciselnik( "SPORT_OSTYP" );
        public List<Polozka> Stavy { get; } = Ciselnik( "SPORT_OSSTAV" );

        public KartaPracovnika(string ft301, int rok)
        {
            this.rok = rok;
            Name = "SmenyKarta";
            Pracovnik = PracovnikSluzba.Nacti( ft301, rok );

            InitializeComponent();
            jisButtonMaxVisible = false;
            jisButtonMinVisible = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DataContext = this;
            Zobrazit();
        }

        /// <summary>
        /// vraci true, pokud byl pracovnik ulozen (i kdyz se karta pak zavrela tlacitkem ZPET)
        /// </summary>
        public bool ShowDialog(Window owner)
        {
            Owner = owner;
            ShowDialog();
            return ulozeno;
        }

        void Zobrazit()
        {
            bool novy = string.IsNullOrEmpty( Pracovnik.Ft301 );
            txtNadpis.Text = novy ? "KARTA PRACOVNÍKA - ZALOŽIT NOVÉHO" : "KARTA PRACOVNÍKA - OPRAVA ÚDAJŮ";
            lblPlan.Content = $"počet dnů dovolené {rok} :";
            txtDovv.Text = $"{Pracovnik.Dovv:0.0}";
            txtVek.Text = $"{PracovnikSluzba.Vek( Pracovnik.Ft305 )}";
            btnDovolena.Visibility = novy ? Visibility.Collapsed : Visibility.Visible;     // VFP: jen u ulozeneho pracovnika
        }

        static List<Polozka> Ciselnik(string sekce)
        {
            SpravaDat.Select( "_t1smeny", "SELECT t1.fitem, t1.fprompt FROM t1 WHERE t1.fsection = @sekce AND t1.f0 = '' ORDER BY t1.fsort", new { sekce }, hide: true );
            return Goa.TableEnum( "_t1smeny" ).Select( r => new Polozka { Kod = $"{r[ "fitem" ]}".Trim(), Nazev = $"{r[ "fprompt" ]}".Trim() } ).ToList();
        }

        // celé jméno se predvyplni jmenem (VFP SPORT1_FT302VALID)
        private void Jmeno_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace( Pracovnik.Ft303 ) && !string.IsNullOrWhiteSpace( Pracovnik.Ft302 ))
                txtCeleJmeno.Text = Pracovnik.Ft302.Trim();
        }

        private void Narozeni_Changed(object sender, SelectionChangedEventArgs e)
        {
            txtVek.Text = $"{PracovnikSluzba.Vek( Pracovnik.Ft305 )}";
        }

        private void BtnUlozit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (new WaitCursor())
                    PracovnikSluzba.Uloz( Pracovnik, rok );
                ulozeno = true;
                Close();
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
        }

        private void BtnDovolena_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                new Dovolena( Pracovnik.Ft301, Pracovnik.Ft302, rok ).ShowDialog( this );
                Pracovnik.Dovv = PracovnikSluzba.Dovolena( Pracovnik.Ft301, rok, PracovnikSluzba.Cerpani );
                txtDovv.Text = $"{Pracovnik.Dovv:0.0}";
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
        }

        private void BtnZpet_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public class Polozka
        {
            public string Kod { get; set; }
            public string Nazev { get; set; }
        }
    }
}
