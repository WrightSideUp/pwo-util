using Microsoft.Azure.Search.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SearchTest.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/
        private AssetsSearch search = new AssetsSearch();

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Search(string q = "")
        {
            // If blank search, assume they want to search everything
            if (string.IsNullOrWhiteSpace(q))
                q = "*";

            var data = search.Search(q).Results;

            return new JsonResult
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = search.Search(q).Results
            };
        }
    }
}