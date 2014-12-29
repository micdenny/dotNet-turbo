﻿using Qoollo.Turbo.ObjectPools.Common;
using Qoollo.Turbo.ObjectPools.ServiceStuff.ElementContainers;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Qoollo.Turbo.ObjectPools
{
    /// <summary>
    /// Пул с ручным заполнением элементами
    /// </summary>
    /// <typeparam name="TElem">Тип элементов пула</typeparam>
    public class StaticPoolManager<TElem> : ObjectPoolManager<TElem>, IPoolElementOperationSource<TElem>
    {
        /// <summary>
        /// Вызов Dispose у элемента
        /// </summary>
        /// <param name="element">Элемент</param>
        private static void CallElementDispose(TElem element)
        {
            IDisposable disposableElem = element as IDisposable;
            if (disposableElem != null)
                disposableElem.Dispose();
        }


        private readonly string _name;
        private readonly Action<TElem> _destroyAction;

        private readonly SimpleElementsContainer<TElem> _elementsContainer;

        private readonly CancellationTokenSource _disposeCancellation;


        /// <summary>
        /// Конструктор StaticPoolManager
        /// </summary>
        /// <param name="name">Имя пула</param>
        /// <param name="destroyAction">Действие, выполняемое при уничтожении элемента</param>
        public StaticPoolManager(string name, Action<TElem> destroyAction)
        {
            _name = name ?? this.GetType().GetCSFullName();
            _destroyAction = destroyAction;

            _elementsContainer = new SimpleElementsContainer<TElem>();
            _disposeCancellation = new CancellationTokenSource();

            Profiling.Profiler.ObjectPoolCreated(this.Name);
        }
        /// <summary>
        /// Конструктор StaticPoolManager
        /// </summary>
        /// <param name="name">Имя пула</param>
        /// <param name="disposeElementOnDestroy">Вызывать ли Dispose у элементов при их уничтожении</param>
        public StaticPoolManager(string name, bool disposeElementOnDestroy)
            : this(name, disposeElementOnDestroy ? new Action<TElem>(CallElementDispose) : null)
        {
        }
        /// <summary>
        /// Конструктор StaticPoolManager
        /// </summary>
        /// <param name="name">Имя пула</param>
        public StaticPoolManager(string name)
            : this(name, null)
        {
        }
        /// <summary>
        /// Конструктор StaticPoolManager
        /// </summary>
        public StaticPoolManager()
            : this(null, null)
        {
        }


        /// <summary>
        /// Имя пула
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Общее число элементов
        /// </summary>
        public override int ElementCount
        {
            get { return _elementsContainer.Count; }
        }

        /// <summary>
        /// Число свободных в данный момент элементов
        /// </summary>
        public int FreeElementCount
        {
            get { return _elementsContainer.AvailableCount; }
        }
        /// <summary>
        /// Число арендованных элементов
        /// </summary>
        private int RentedElementCount
        {
            get { return this.ElementCount - this.FreeElementCount; }
        }


        /// <summary>
        /// Добавление нового элемента в пул
        /// </summary>
        /// <param name="elem">Элемент</param>
        public void AddElement(TElem elem)
        {
            if (_disposeCancellation.IsCancellationRequested)
                throw new ObjectDisposedException(this.GetType().Name);
            if (this.ElementCount >= (1 << 16) - 2)
                throw new InvalidOperationException("Max supported ElementCount is 65534");

            var addedElem = _elementsContainer.Add(elem, this, true);
            addedElem.SetPoolName(this.Name);
            Profiling.Profiler.ObjectPoolElementCreated(this.Name, this.ElementCount);
        }


        /// <summary>
        /// Удаление арендованного элемента из пула (напрмер, если стал не валиден)
        /// </summary>
        /// <param name="elemMonitor">Обёртка над элементом</param>
        public void RemoveElement(RentedElementMonitor<TElem> elemMonitor)
        {
            if (elemMonitor.IsDisposed)
                throw new ArgumentException("Element from 'elemMonitor' already returned to ObjectPool (" + this.Name + ")", "elemMonitor");
            if (!object.ReferenceEquals(this, elemMonitor.SourcePool))
                throw new ArgumentException("RentedElementMonitor is not belog to current ObjectPool (" + this.Name + ")", "elemMonitor");

            DestroyElementInner(elemMonitor.ElementWrapper);
            Profiling.Profiler.ObjectPoolElementDestroyed(this.Name, this.ElementCount);
        }



        /// <summary>
        /// Освобождение ресурсов элемента (точка расширения)
        /// </summary>
        /// <param name="elem">Элемент</param>
        protected virtual void DestroyElement(TElem elem)
        {
            if (_destroyAction != null)
                _destroyAction(elem);
        }

        /// <summary>
        /// Набор действий для уничтожения элемента
        /// </summary>
        /// <param name="element">Элемент</param>
        private void DestroyElementInner(PoolElementWrapper<TElem> element)
        {
            Contract.Requires(element != null);
            Contract.Requires(element.IsBusy);
            Contract.Requires(!element.IsElementDestroyed);

            DestroyElement(element.Element);
            element.MarkElementDestroyed();
        }

        /// <summary>
        /// Уничтожить и удалить элемент
        /// </summary>
        /// <param name="element">Элемент</param>
        private void DestroyAndRemoveElement(PoolElementWrapper<TElem> element)
        {
            Contract.Requires(element != null);
            Contract.Requires(element.IsBusy);
            Contract.Requires(!element.IsElementDestroyed);

            try
            {
                DestroyElementInner(element);
            }
            finally
            {
                _elementsContainer.Release(element);
            }

            Profiling.Profiler.ObjectPoolElementDestroyed(this.Name, this.ElementCount);
        }

        /// <summary>
        /// Забрать свободный элемент, уничтожить и удалить
        /// </summary>
        /// <returns>Был ли свободный элемент</returns>
        private bool TakeDestroyAndRemoveElement()
        {
            PoolElementWrapper<TElem> element = null;
            if (_elementsContainer.TryTake(out element, 0, new CancellationToken()))
            {
                DestroyAndRemoveElement(element);
                return true;
            }

            return false;
        }







        /// <summary>
        /// Арендовать элемент
        /// </summary>
        /// <param name="timeout">Таймаут (-1 - бесконечно, 0 - быстрая проверка, > 0 - полная проверка)</param>
        /// <param name="token">Токен для отмены</param>
        /// <param name="throwOnUnavail">Выбрасывать исключение при недоступности элемента</param>
        /// <returns>Обёртка над элементом пула</returns>
        protected sealed override PoolElementWrapper<TElem> RentElement(int timeout, CancellationToken token, bool throwOnUnavail)
        {
            if (_disposeCancellation.IsCancellationRequested)
            {
                if (throwOnUnavail)
                    throw new ObjectDisposedException(this.GetType().Name);

                return null;
            }

            if (token.IsCancellationRequested)
            {
                if (throwOnUnavail)
                    token.ThrowIfCancellationRequested();

                return null;
            }


            PoolElementWrapper<TElem> result = null;
            bool elemWasTaken = false;

            if (timeout < 0)
                timeout = Timeout.Infinite;

            CancellationTokenSource linkedTokenSource = null;

            try
            {
                if (timeout == 0)
                {
                    elemWasTaken = _elementsContainer.TryTake(out result, 0, new CancellationToken());
                }
                else if (!token.CanBeCanceled)
                {
                    elemWasTaken = _elementsContainer.TryTake(out result, timeout, _disposeCancellation.Token);
                }
                else
                {
                    linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, _disposeCancellation.Token);
                    elemWasTaken = _elementsContainer.TryTake(out result, timeout, linkedTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (linkedTokenSource != null)
                    linkedTokenSource.Dispose();
            }


            if (throwOnUnavail && !elemWasTaken)
            {
                if (_disposeCancellation.IsCancellationRequested)
                    throw new CantRetrieveElementException("Rent from pool failed. Dispose was called.", new ObjectDisposedException(this.GetType().Name));

                token.ThrowIfCancellationRequested();

                if (timeout >= 0)
                    throw new TimeoutException(string.Format("Pool 'Rent' operation has timeouted. Pool: {0}. Timeout value: {1}ms", this.Name, timeout));

                Contract.Assert(false, "Element in pool is not available. Reason: UNKNOWN!");
                throw new CantRetrieveElementException("Rent from pool failed");
            }


            Profiling.Profiler.ObjectPoolElementRented(this.Name, this.RentedElementCount);
            return result;
        }



        /// <summary>
        /// Вернуть элемент в пул
        /// </summary>
        /// <param name="element">Элемент</param>
        protected internal sealed override void ReleaseElement(PoolElementWrapper<TElem> element)
        {
            if (!element.IsBusy)
                throw new InvalidOperationException("Trying to release same element several times in Pool: " + this.Name);

            if (_disposeCancellation.IsCancellationRequested)
                DestroyAndRemoveElement(element);
            else
                _elementsContainer.Release(element);

            Profiling.Profiler.ObjectPoolElementReleased(this.Name, this.RentedElementCount);
        }




        /// <summary>
        /// Является ли элемент валидным
        /// </summary>
        /// <param name="container">Контейнер элемента</param>
        /// <returns>Является ли валидным</returns>
        bool IPoolElementOperationSource<TElem>.IsValid(PoolElementWrapper<TElem> container)
        {
            return true;
        }



        /// <summary>
        /// Основной код освобождения ресурсов
        /// </summary>
        /// <param name="isUserCall">Вызвано ли освобождение пользователем. False - деструктор</param>
        protected override void Dispose(bool isUserCall)
        {
            if (isUserCall)
            {
                if (!_disposeCancellation.IsCancellationRequested)
                {
                    _disposeCancellation.Cancel();

                    try { }
                    finally
                    {
                        int count = _elementsContainer.Count;
                        while (TakeDestroyAndRemoveElement())
                            Contract.Assert(--count >= 0);
                    }

                    Profiling.Profiler.ObjectPoolDisposed(this.Name, false);
                }
            }

            base.Dispose(isUserCall);
        }
    }
}