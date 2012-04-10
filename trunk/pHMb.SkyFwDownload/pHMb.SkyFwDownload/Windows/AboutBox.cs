//Copyright (C) 2012 Matthew Thornhill (mrmt32@ph-mb.com)

//This program is free software; you can redistribute it and/or
//modify it under the terms of the GNU General Public License
//as published by the Free Software Foundation; either version 2
//of the License, or (at your option) any later version.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.

//You should have received a copy of the GNU General Public License
//along with this program; if not, write to the Free Software
//Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace pHMb.TS_Demux
{
    partial class AboutBox : Form
    {
        public AboutBox()
        {
            InitializeComponent();

            this.Text = String.Format("About {0}", AssemblyTitle);
            this.labelProductName.Text = string.Format("{0} is Copyright \u00a9 pH-Mb 2011, All rights reserved.", AssemblyProduct);
            this.labelVersion.Text = string.Format("Installed Version: {0}", AssemblyVersion);
            this.lblSupport.Text = string.Format("For help and support please visit http://www.ph-mb.com/\n or email help@ph-mb.com.");
            this.lblSupport.LinkArea = new LinkArea(34, 22);

            List<Assembly> assembliesLoaded = (from assembly in AppDomain.CurrentDomain.GetAssemblies()
                                           where
                                             assembly.ManifestModule.Name != "<In Memory Module>"
                                             && !assembly.FullName.StartsWith("System")
                                             && !assembly.FullName.StartsWith("mscorlib")
                                             && !assembly.FullName.StartsWith("vshost")
                                             && !assembly.FullName.StartsWith("WindowsBase")
                                             && !assembly.FullName.StartsWith("Accessibility")
                                             && !assembly.FullName.StartsWith("Presentation")
                                             && !assembly.FullName.StartsWith("Microsoft")
                                             && assembly.Location.IndexOf("App_Web") == -1
                                             && assembly.Location.IndexOf("App_global") == -1
                                             && assembly.FullName.IndexOf("CppCodeProvider") == -1
                                             && assembly.FullName.IndexOf("WebMatrix") == -1
                                             && assembly.FullName.IndexOf("SMDiagnostics") == -1
                                             && !String.IsNullOrEmpty(assembly.Location)
                                           select assembly).ToList();

            foreach (Assembly assembly in assembliesLoaded)
            {
                listBoxVersions.Items.Add(string.Format("{0} - {1}", assembly.GetName().Name, assembly.GetName().Version));
            }
        }

        #region Assembly Attribute Accessors

        public string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != "")
                    {
                        return titleAttribute.Title;
                    }
                }
                return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public string AssemblyVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public string AssemblyDescription
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyDescriptionAttribute)attributes[0]).Description;
            }
        }

        public string AssemblyProduct
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        public string AssemblyCopyright
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        public string AssemblyCompany
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCompanyAttribute)attributes[0]).Company;
            }
        }
        #endregion

        private void lblSupport_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            lblSupport.LinkVisited = true;
            System.Diagnostics.Process.Start("http://www.ph-mb.com");
        }
    }
}
