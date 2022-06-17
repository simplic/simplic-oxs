using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Data
{
    /// <summary>
    /// Provides extension methods for <see cref="MapService"/>.
    /// </summary>
    public static class MapServiceExtension
    {
        /// <summary>
        /// Copies the value of a property defined by a lambda expression from the original to the target object.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="expression">A function mapping the original object to one of its properties to be copied to the target object.</param>
        /// <returns></returns>
        public static MergeableObject<T> Take<T, U>(this MergeableObject<T> _this, Expression<Func<T, U>> expression) where T : class
        {
            if (expression.Body is MemberExpression memberExpression)
            {
                var property = (PropertyInfo) memberExpression.Member;

                if (property != null)
                {
                    property.SetValue(_this.Target, property.GetValue(_this.Original));
                    return _this;
                }
            }

            throw new ArgumentException(nameof(expression));
        }

        /// <summary>
        /// Copies default property values from the original to the target object.
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="_this"></param>
        /// <returns></returns>
        public static MergeableObject<T> TakeDefaults<T>(this MergeableObject<T> _this) where T : class
        {
            var propertyNames = new List<string>
            {
                "Id", "CreateUser", "CreateTime"
            };

            foreach (var name in propertyNames)
            {
                var property =_this.Original.GetType().GetProperty(name);

                if (property != null)
                    property.SetValue(_this.Target, property.GetValue(_this.Original));
            }

            return _this;
        }
    }
}
