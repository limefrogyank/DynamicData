using System;

namespace DynamicData.Tests.Domain
{
    public class PersonWithEmployment : IDisposable
    {
        private readonly IGroup<PersonEmployment, PersonEmpKey, string> _source;

        public PersonWithEmployment(IGroup<PersonEmployment, PersonEmpKey, string> source)
        {
            _source = source;
            EmploymentData = source.Cache;
        }

        public string Person => _source.Key;

        public IObservableCacheAsync<PersonEmployment, PersonEmpKey> EmploymentData { get; }

        public int EmploymentCount => EmploymentData.Count;

        public void Dispose()
        {
            EmploymentData.Dispose();
        }

        public override string ToString()
        {
            return $"Person: {Person}. Count {EmploymentCount}";
        }
    }
}
