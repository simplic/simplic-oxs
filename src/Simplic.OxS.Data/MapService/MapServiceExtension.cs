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
                var property = memberExpression.Member as PropertyInfo;

                if (SetValueIfPropertyExists(property, _this.Original, _this.Target))
                    return _this;
            }

            throw new ArgumentException(nameof(expression));
        }

        /// <summary>
        /// Copies default property values from the original to the target object.
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <typeparam name="I">The type of the interface defining the object's default properties</typeparam>
        /// <returns></returns>
        public static MergeableObject<T> TakeDefaults<T, I>(this MergeableObject<T> _this) 
                                                            where I : IDefaultDocument 
                                                            where T : class
        {
            foreach (var property in typeof(I).GetProperties())
                SetValueIfPropertyExists(property, _this.Original, _this.Target);

            return _this;
        }

        /// <summary>
        /// Copies the value of a property from the original to the target object.
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="property">The property to copy</param>
        /// <param name="original">The original object</param>
        /// <param name="target">The target object</param>
        /// <returns>Whether the property exists or not</returns>
        private static bool SetValueIfPropertyExists<T>(PropertyInfo property, T original, T target)
        {
            if (property == null)
                return false;

            property.SetValue(target, property.GetValue(original));
            return true;
        }
    }
}
