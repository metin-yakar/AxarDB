using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AxarDB.Sdk
{
    public class AxarQueryBuilder<T>
    {
        private readonly AxarClient _client;
        private readonly string _collection;
        private readonly List<string> _whereClauses = new List<string>();
        private readonly Dictionary<string, object> _parameters = new Dictionary<string, object>();
        private int _paramCounter = 0;
        private int? _take;

        public AxarQueryBuilder(AxarClient client, string collection)
        {
            _client = client;
            _collection = collection;
        }

        public AxarQueryBuilder<T> Where(string property, string op, object value)
        {
            var paramName = $"p{_paramCounter++}";
            _whereClauses.Add($"x.{property} {op} @{paramName}");
            _parameters[paramName] = value;
            return this;
        }

        public AxarQueryBuilder<T> Where(string rawExpression, object parameters = null)
        {
             if (parameters != null)
             {
                 foreach (var prop in parameters.GetType().GetProperties())
                 {
                     _parameters[prop.Name] = prop.GetValue(parameters);
                 }
             }
             _whereClauses.Add(rawExpression);
             return this;
        }

        public AxarQueryBuilder<T> Take(int count)
        {
            _take = count;
            return this;
        }

        /// <summary>
        /// Projects each element of a sequence into a new form.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="selector">A projection function string (e.g. "x => x.name")</param>
        public AxarQueryBuilder<TResult> Select<TResult>(string selector)
        {
            // We need to pass the state to the new builder
            var newBuilder = new AxarQueryBuilder<TResult>(_client, _collection);
            newBuilder._whereClauses.AddRange(_whereClauses);
            foreach(var p in _parameters) newBuilder._parameters.Add(p.Key, p.Value);
            newBuilder._paramCounter = _paramCounter;
            newBuilder._take = _take;
            newBuilder._selector = selector;
            return newBuilder;
        }

        /// <summary>
        /// Projects each element of a sequence into a new form and returns the list asynchronously.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="selector">A projection function string (e.g. "x => x.name")</param>
        public async System.Threading.Tasks.Task<List<TResult>> SelectAsync<TResult>(string selector)
        {
            return await Select<TResult>(selector).ToListAsync();
        }

        private string _selector;

        private string BuildBaseScript()
        {
            var sb = new StringBuilder($"db.{_collection}");
            
            if (_whereClauses.Any())
            {
                var predicate = string.Join(" && ", _whereClauses);
                sb.Append($".findall(x => {predicate})");
            }
            else
            {
                sb.Append(".findall()");
            }

            if (_take.HasValue)
            {
                sb.Append($".take({_take.Value})");
            }

            if (!string.IsNullOrEmpty(_selector))
            {
                sb.Append($".select({_selector})");
            }

            return sb.ToString();
        }

        public async System.Threading.Tasks.Task<List<T>> ToListAsync()
        {
            var script = BuildBaseScript() + ".toList()";
            return await _client.QueryAsync<List<T>>(script, _parameters);
        }

        public async System.Threading.Tasks.Task<T> FirstAsync()
        {
            var script = BuildBaseScript() + ".first()";
            return await _client.QueryAsync<T>(script, _parameters);
        }

        public async System.Threading.Tasks.Task<int> CountAsync()
        {
            var script = BuildBaseScript() + ".count()";
            return await _client.QueryAsync<int>(script, _parameters);
        }

        public async System.Threading.Tasks.Task UpdateAsync(object updateData)
        {
            // Update takes a parameter for the update object
            var paramName = $"update_{_paramCounter++}";
            _parameters[paramName] = updateData;
            
            var script = BuildBaseScript() + $".update(@{paramName})";
            await _client.ExecuteAsync(script, _parameters);
        }

        public async System.Threading.Tasks.Task DeleteAsync()
        {
            var script = BuildBaseScript() + ".delete()";
            await _client.ExecuteAsync(script, _parameters);
        }

    }
}
