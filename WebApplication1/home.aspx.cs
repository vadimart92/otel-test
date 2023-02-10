using System;

namespace WebApplication1
{
	public class home_aspx : System.Web.UI.Page
	{
		protected override void OnInit(EventArgs e) {
			base.OnInit(e);
		}

		public void Get() {
			Response.Write("OK");
		}
	}
}
