using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DynamicData.SignalR.TestModel;
using DynamicData.SignalR.TestWebApp.Data;
using Microsoft.AspNetCore.Mvc;

// This controller is NOT necessary for the DynamicData/SignalR SourceCache.  
// It's only here to verify information written to the database.


namespace DynamicData.SignalR.TestWebApp.Controllers
{    
    [Route("api/[controller]")]
    public class PersonsController : Controller
    {
        TestContext dbContext = new TestContext();
        // GET: api/<controller>
        [HttpGet]
        public IEnumerable<Person> Get()
        {
            return dbContext.Persons.ToList();
        }

        // GET api/<controller>/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<controller>
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }

        // PUT api/<controller>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/<controller>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
