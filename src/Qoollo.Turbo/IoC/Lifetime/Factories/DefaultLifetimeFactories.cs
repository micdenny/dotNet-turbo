﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Qoollo.Turbo.IoC.Helpers;
using Qoollo.Turbo.IoC.ServiceStuff;

namespace Qoollo.Turbo.IoC.Lifetime.Factories
{
    /// <summary>
    /// Базовый класс для фабрики создания объектов управления временем жизни
    /// </summary>
    [ContractClass(typeof(LifetimeFactoryCodeContractCheck))]
    public abstract class LifetimeFactory
    {
        /// <summary>
        /// Создание объекта управления временем жизни
        /// </summary>
        /// <param name="objType">Тип хранимого объекта</param>
        /// <param name="injection">Резолвер инъекций</param>
        /// <param name="extInfo">Расширенная информация (если есть)</param>
        /// <returns>Объект управления жизнью объекта</returns>
        public abstract LifetimeBase Create(Type objType, IInjectionResolver injection, object extInfo);
    }

    /// <summary>
    /// Фабрика для синглтона (SingletonLifetime)
    /// </summary>
    public sealed class SingletonLifetimeFactory: LifetimeFactory
    {
        /// <summary>
        /// Создание объекта управления временем жизни
        /// </summary>
        /// <param name="objType">Тип хранимого объекта</param>
        /// <param name="injection">Резолвер инъекций</param>
        /// <param name="extInfo">Расширенная информация (если есть)</param>
        /// <returns>Объект управления жизнью объекта</returns>
        public override LifetimeBase Create(Type objType, IInjectionResolver injection, object extInfo)
        {
            var obj = OnjectInstantiationHelper.CreateObject(objType, injection, extInfo);
            return new SingletonLifetime(obj);
        }
    }

    /// <summary>
    /// Фабрика для синглтона с отложенной инициализацией (DeferedSingletonLifetime)
    /// </summary>
    public sealed class DeferedSingletonLifetimeFactory : LifetimeFactory
    {
        /// <summary>
        /// Создание объекта управления временем жизни
        /// </summary>
        /// <param name="objType">Тип хранимого объекта</param>
        /// <param name="injection">Резолвер инъекций</param>
        /// <param name="extInfo">Расширенная информация (если есть)</param>
        /// <returns>Объект управления жизнью объекта</returns>
        public override LifetimeBase Create(Type objType, IInjectionResolver injection, object extInfo)
        {
            var creationFunc = OnjectInstantiationHelper.GetReflectionBasedCreationFunction(objType, extInfo);
            return new DeferedSingletonLifetime(creationFunc, objType);
        }
    }

    /// <summary>
    /// Фабрика для PerThreadLifetime
    /// </summary>
    public sealed class PerThreadLifetimeFactory : LifetimeFactory
    {
        /// <summary>
        /// Создание объекта управления временем жизни
        /// </summary>
        /// <param name="objType">Тип хранимого объекта</param>
        /// <param name="injection">Резолвер инъекций</param>
        /// <param name="extInfo">Расширенная информация (если есть)</param>
        /// <returns>Объект управления жизнью объекта</returns>
        public override LifetimeBase Create(Type objType, IInjectionResolver injection, object extInfo)
        {
            var creationFunc = OnjectInstantiationHelper.GetCompiledCreationFunction(objType, extInfo);
            return new PerThreadLifetime(creationFunc, objType);
        }
    }

    /// <summary>
    /// Фабрика для PerCallInterfaceLifetime
    /// </summary>
    public sealed class PerCallLifetimeFactory : LifetimeFactory
    {
        /// <summary>
        /// Создание объекта управления временем жизни
        /// </summary>
        /// <param name="objType">Тип хранимого объекта</param>
        /// <param name="injection">Резолвер инъекций</param>
        /// <param name="extInfo">Расширенная информация (если есть)</param>
        /// <returns>Объект управления жизнью объекта</returns>
        public override LifetimeBase Create(Type objType, IInjectionResolver injection, object extInfo)
        {
            var creationObj = OnjectInstantiationHelper.BuildInstanceCreatorInDynAssembly(objType, extInfo);
            return new PerCallInterfaceLifetime(objType, creationObj);
        }
    }

    /// <summary>
    /// Фабрика для PerCallInlinedParamsInterfaceLifetime
    /// </summary>
    public sealed class PerCallInlinedParamsLifetimeFactory : LifetimeFactory
    {
        /// <summary>
        /// Создание объекта управления временем жизни
        /// </summary>
        /// <param name="objType">Тип хранимого объекта</param>
        /// <param name="injection">Резолвер инъекций</param>
        /// <param name="extInfo">Расширенная информация (если есть)</param>
        /// <returns>Объект управления жизнью объекта</returns>
        public override LifetimeBase Create(Type objType, IInjectionResolver injection, object extInfo)
        {
            var creationObj = OnjectInstantiationHelper.BuildInstanceCreatorNoParamInDynAssembly(objType, injection, extInfo);
            return new PerCallInlinedParamsInterfaceLifetime(objType, creationObj);
        }
    }









    /// <summary>
    /// Контракты
    /// </summary>
    [ContractClassFor(typeof(LifetimeFactory))]
    abstract class LifetimeFactoryCodeContractCheck : LifetimeFactory
    {
        /// <summary>Контракты</summary>
        private LifetimeFactoryCodeContractCheck() { }


        /// <summary>Контракты</summary>
        public override LifetimeBase Create(Type objType, IInjectionResolver injection, object extInfo)
        {
            Contract.Requires<ArgumentNullException>(objType != null, "objType != null");
            Contract.Requires<ArgumentNullException>(injection != null, "injection != null");
            Contract.Ensures(Contract.Result<LifetimeBase>() != null);
            Contract.Ensures(Contract.Result<LifetimeBase>().OutputType == objType);

            throw new NotImplementedException();
        }
    }

}
