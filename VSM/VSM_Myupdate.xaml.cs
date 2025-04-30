using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VRisingServerManager
{
    /// <summary>
    /// VSM_Myupdate.xaml 的交互逻辑
    /// </summary>
    public partial class VSM_Myupdate : Window
    {
        public VSM_Myupdate()
        {
            InitializeComponent();
            for (int i = 0; i < 6; i++)
            {
                ProgressBar2.Value++;
                //为看出效果可以加个  
                Thread.Sleep(1000);
            }
        }
    }
}
