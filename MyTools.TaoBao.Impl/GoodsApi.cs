﻿/*
 *名称：GoodsApi
 *功能：
 *创建人：吉桂昕
 *创建时间：2013-05-11 05:37:44
 *修改时间：
 *备注：
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Infrastructure.Crosscutting.Declaration;
using Infrastructure.Crosscutting.IoC;
using Infrastructure.Crosscutting.Logging;
using Infrastructure.Crosscutting.Utility;
using Infrastructure.Crosscutting.Utility.CommomHelper;
using MyTools.Framework.Common;
using MyTools.Framework.Common.ExceptionDef;
using MyTools.TaoBao.DomainModule;
using MyTools.TaoBao.Interface;
using Top.Api;
using Top.Api.Domain;
using Top.Api.Request;
using Top.Api.Response;
using Top.Api.Util;
using Product = MyTools.TaoBao.DomainModule.Product;

namespace MyTools.TaoBao.Impl
{
    using System.Drawing;
    using System.Drawing.Imaging;
    
    public class GoodsApi : IGoodsApi,IGoodsPublish
    {
        #region Members
         
        private readonly ICatalog _catalog = InstanceLocator.Current.GetInstance<ICatalog>(Resource.SysConfig_GetDataWay);
        private readonly ITopClient _client = InstanceLocator.Current.GetInstance<ITopClient>();
         
        private readonly IDelivery _delivery =
            InstanceLocator.Current.GetInstance<IDelivery>(Resource.SysConfig_GetDataWay);

        private readonly Dictionary<string, string> _dicColorMap = new Dictionary<string, string>();

        private readonly ILogger _log = InstanceLocator.Current.GetInstance<ILoggerFactory>().Create();
      
        public List<SellerCat> SellercatsList;

        private ImageWatermark imageWatermark = new ImageWatermark();

        #endregion

        #region Constructor

        #endregion

        #region public Methods

        #region IGoodsPublish 

        public Item PublishGoods(string sUrl, IRequest req)
        {
            string goodsSn = req.GetGoodsSn(sUrl);

            Item item = VerifyGoodsExist(goodsSn);
            if (item.IsNotNull())
            {
                //修改库存信息 
                UpdateGoodsInternalSingle(req,item, sUrl, false);

                return item;
            }
             
            return PublishGoodsMain(req, new RequestModel { GoodsSn = goodsSn, Referer = sUrl });
        }

        public void UpdateGoodsFromOnSale(IRequest req, IEnumerable<string> lstSearch, bool isModifyPrice = true)
        {
            foreach (var search in lstSearch)
            {
                if (string.IsNullOrWhiteSpace(search))
                    continue;
                UpdateGoodsFromOnSale(req,search, isModifyPrice);
            } 
        }

        public void UpdateGoodsSkuInfo(
            IRequest reqSource,
            IEnumerable<string> lstSearch,  //要为款号
            double discountRatio = 0.68,
            int stock = 3,
            string originalTitle = "xx",
            string newTitle = "xx",
            bool isModifyPrice = true)
        {
            foreach (var search in lstSearch)
            {
                if (search.IsEmptyString()) continue;

                List<Item> lstItem = GetOnSaleGoods(search);
                if (lstItem != null && lstItem.Count > 0)
                {
                    #region 更新SKU信息

                    foreach (var item in lstItem)
                    {
                        Thread.Sleep(100);
                        //获取产品原价
                        var oPrice = item.Title.GetNumberInt();

                        var req = new ItemUpdateRequest();
                        req.NumIid = item.NumIid;
                        req.Title = item.Title.Replace(originalTitle, newTitle);

                        req.LocationState = SysConst.LocationState;
                        req.LocationCity = SysConst.LocationCity;
                        if (isModifyPrice)
                        {
                            req.Price = (oPrice * discountRatio).ToType<int>().ToString();
                        }

                        var skus = GetSkusByNumId(item.NumIid.ToString()).ToArray();
                        var lastSku = skus[skus.Count() - 1];
                        for (int i = 0; i < skus.Count() - 1; i++)
                        {
                            Thread.Sleep(100);
                            var skuReq = new ItemSkuUpdateRequest()
                                             {
                                                 NumIid = item.NumIid,
                                                 Properties = skus[i].Properties,
                                                 Quantity = stock,
                                                 OuterId = item.OuterId
                                             };
                            if (isModifyPrice)
                            {
                                skuReq.Price = req.Price;
                            }
                            UpdateSku(skuReq);
                            Thread.Sleep(100);
                        }

                        UpdateGoodsBase(req, item.NumIid, item.OuterId, req.Title);

                        var skuLastReq = new ItemSkuUpdateRequest()
                                             {
                                                 NumIid = item.NumIid,
                                                 Properties = lastSku.Properties,
                                                 Quantity = stock,
                                                 OuterId = item.OuterId
                                             };
                        if (isModifyPrice)
                        {
                            skuLastReq.Price = req.Price;
                        }
                        UpdateSku(skuLastReq);
                    }

                    #endregion
                }
                else
                { 
                    PublishGoodsMain(
                        reqSource,
                        new RequestModel { GoodsSn = search, Referer = reqSource.GetGoodsUrl(search) });
                }
            } 
        }

        //发布商品的主方法
        private Item PublishGoodsMain(IRequest req, RequestModel requestModel)
        {
            try
            {
                Product product = req.GetGoodsInfo(requestModel);
                product.SetAddProperty();

                if (product.ColorList.IsNullOrEmpty()) return null;

                req.SetCidAndSellerCids(product);

                return PublishGoodsAndUploadPic(product);
            }
            catch (Exception e)
            {
                _log.LogError("发布该产品失败，邦购没有该产品:{0}", e, requestModel.GoodsSn);
                return null;
            }
        }

        #endregion

        #region PublishGoods
         
        /// <summary>
        /// 重EXCEL中发布产品
        /// </summary>
        /// <param name="filePath"></param>
        public void PublishGoodsFromExcel(string filePath)
        {
            //IEnumerable<PublishGoods> lstPublishGoods = GetPublishGoodsFromExcel(filePath);

            //foreach (PublishGoods pgModel in lstPublishGoods)
            //{
            //    Thread.Sleep(500);

            //    Item item = VerifyGoodsExist(pgModel.GoodsSn);
            //    if (item.IsNotNull())
            //    {
            //        #region 更新现有商品

            //        try
            //        {
            //            var banggoProduct = new BanggoProduct(false)
            //            {
            //                ColorList = pgModel.ProductColors,
            //                GoodsSn = item.OuterId,
            //                GoodsUrl = pgModel.Url,
            //                Cid = item.Cid,
            //                NumIid = item.NumIid,
            //                //替换原来的产品标题
            //                Title = item.Title.Replace(SysConst.OriginalTitle, SysConst.NewTitle)
            //            };
            //            //  Util.CopyModel(item, banggoProduct); node: 不能在这赋值，这样就会造成有些为NULL的给赋成了默认值 
            //            var req = new BanggoRequestModel { GoodsSn = banggoProduct.GoodsSn, Referer = banggoProduct.GoodsUrl };

            //            banggoProduct.BSizeToTSize = _banggoMgt.GetBSizeToTSize(_banggoMgt.GetGoodsDetialElementData(req));

            //            DeleteAllSku(item); 

            //            UpdateGoodsAndUploadPic(banggoProduct);
            //        }
            //        catch (Exception ex)
            //        {
            //            _log.LogError(Resource.Log_UpdateGoodsFailure.StringFormat(item.NumIid, item.OuterId), ex);
            //            continue;
            //        }

            //        #endregion
            //    }
            //    else
            //    {
            //        #region 发布商品 从EXCEL中读取

            //        try
            //        {
            //            var product = new BanggoProduct { GoodsSn = pgModel.GoodsSn };

            //            var requestModel = new BanggoRequestModel { Referer = pgModel.Url, GoodsSn = pgModel.GoodsSn };

            //            _banggoMgt.GetProductBaseInfo(product, requestModel);

            //            _banggoMgt.GetProductSkuBase(product, requestModel);

            //            product.ColorList = pgModel.ProductColors;

            //            PublishGoodsAndUploadPic(product);
            //        }
            //        catch (Exception ex)
            //        {
            //            _log.LogError(Resource.Log_PublishGoodsFailure.StringFormat(pgModel.GoodsSn), ex);
            //            continue;
            //        }

            //        #endregion
            //    }
            //    Thread.Sleep(500);
            //}
        }
          
        //发布商品并上传图片
        private Item PublishGoodsAndUploadPic(Product product)
        {
            StuffProductInfo(product);

            Item item = PublishGoods(product);

            foreach (ProductColor pColor in product.ColorList)
            {
                UploadItemPropimg(item.NumIid, pColor.MapProps, new Uri(pColor.ImgUrl));
                Thread.Sleep(500);
            }
            return item;
        }

        /// <summary>
        ///     发布商品
        /// taobao.item.add 添加一个商品 
        /// </summary>
        /// <param name="product">商品</param>
        /// <returns>商品编号</returns>
        public Item PublishGoods(Product product)
        {
            _log.LogInfo(Resource.Log_PublishGoodsing.StringFormat(product.Title));

            var req = new ItemAddRequest();

            Util.CopyModel(product, req);

            var tContext = InstanceLocator.Current.GetInstance<TopContext>();
            ItemAddResponse response = _client.Execute(req, tContext.SessionKey);

            if (response.IsError)
            {
                var ex = new TopResponseException(response.ErrCode, response.ErrMsg, response.SubErrCode,
                                                  response.SubErrMsg, response.TopForbiddenFields);
                _log.LogError(Resource.Log_PublishGoodsFailure, product.Title, ex);
                throw ex;
            }

            Item item = response.Item;

            _log.LogInfo(Resource.Log_PublishGoodsSuccess, product.Title, item.NumIid);

            return item;
        }
         
        #endregion

        #region UpdateGoods
           
        /// <summary>
        /// 根据搜索条件查询已上架的商品，并更新相应库存  IRequest
        /// </summary>
        public void UpdateGoodsFromOnSale(IRequest req,string search = null, bool isModifyPrice = true)
        {
            List<Item> lstItem = GetOnSaleGoods(search);

            UpdateGoodsInternal(req,lstItem, isModifyPrice);
        }
         
        /// <summary>
        ///     通过指定部分没有更新成功的商品重新更新
        /// </summary>
        /// <param name="numIds">多个产品以“，”号分割</param>
        /// <param name="isModifyPrice"></param>
        public void UpdateGoodsByAssign(IRequest req, string numIds, bool isModifyPrice = true)
        {
            List<Item> lstItem = GetGoodsList(numIds);

            UpdateGoodsInternal(req, lstItem, isModifyPrice);
        }

        /// <summary>
        /// taobao.item.sku.update 更新SKU信息
        /// </summary>
        /// <returns></returns>
        public Sku UpdateSku(ItemSkuUpdateRequest req)
        {
            _log.LogInfo(Resource.Log_UpdateSkuing.StringFormat(req.NumIid, req.Properties, req.OuterId));

            req.ThrowIfNull(Resource.ExceptionTemplate_MethedParameterIsNullorEmpty.StringFormat(new StackTrace()));

            var tContext = InstanceLocator.Current.GetInstance<TopContext>();

            var response = _client.Execute(req, tContext.SessionKey);

            if (response.IsError)
            {
                var ex = new TopResponseException(response.ErrCode, response.ErrMsg, response.SubErrCode,
                                               response.SubErrMsg, response.TopForbiddenFields);
                _log.LogError(Resource.Log_UpdateSkuFailure.StringFormat(req.NumIid, req.Properties, req.OuterId), ex);

            }

            _log.LogInfo(Resource.Log_UpdateSkuSuccess.StringFormat(req.NumIid, req.Properties, req.OuterId));

            return response.Sku;
        }
         
        /// <summary>
        ///     更新商品信息包括SKU信息,必须保证BSizeToTSize有值
        /// </summary>
        public void UpdateGoodsInfo(Product product)
        {
            //1，填充必填项到props
            string itemProps = _catalog.GetItemProps(product.Cid.ToString());
            product.Props = itemProps; //只先提取必填项
             
            //2，SetSkuInfo 
            SetSkuInfo(product);
            Thread.Sleep(200);

            UpdateGoods(product);
        }
         
        public Item UpdateGoods(Product product)
        {
            var req = new ItemUpdateRequest();

            Util.CopyModel(product, req);

            return UpdateGoodsBase(req, product.NumIid, product.OuterId, product.Title);
        }

        private Item UpdateGoodsBase(ItemUpdateRequest req, long? numiid, string outerId, string title)
        {
            _log.LogInfo(Resource.Log_UpdateGoodsing.StringFormat(numiid, outerId));

            var tContext = InstanceLocator.Current.GetInstance<TopContext>();
            ItemUpdateResponse response = _client.Execute(req, tContext.SessionKey);

            if (response.IsError)
            {
                var ex = new TopResponseException(response.ErrCode, response.ErrMsg, response.SubErrCode,
                                                  response.SubErrMsg, response.TopForbiddenFields);
                _log.LogError(Resource.Log_UpdateGoodsFailure.StringFormat(numiid, outerId), ex);
                throw ex;
            }

            Item item = response.Item;

            _log.LogInfo(Resource.Log_UpdateGoodsSuccess, title, item.NumIid);

            return item;
        }

        //更新商品的内部方法
        private void UpdateGoodsInternal(IRequest req, IEnumerable<Item> lstItem, bool isModifyPrice = true)
        {
            //遍历在售商品列表中的商品，通过outerid去查询banggo上的该产品信息
            foreach (Item item in lstItem)
            {
                Thread.Sleep(1000);

                //通过款号查询如果没有得到产品的URL或得不到库存，就将该产品进行下架。
                string goodsUrl = req.GetGoodsUrl(item.OuterId);

                this.UpdateGoodsInternalSingle(req,item, goodsUrl, isModifyPrice);
            }
        }
         
        //更新单条产品
        private void UpdateGoodsInternalSingle(IRequest req,Item item, string goodsUrl, bool isModifyPrice = true)
        {
            try
            {
                if (goodsUrl.IsNullOrEmpty())
                {
                    // this.GoodsDelisting(item.NumIid); //不进行下架，如在获取产品时，网络问题等因素
                    this._log.LogInfo("GoodsSn:{0}->没有在邦购上获取URL不能进行进行操作".StringFormat(item.OuterId));
                    return;
                }

                var product = new Product
                {
                    GoodsSn = item.OuterId,
                    GoodsUrl = goodsUrl,
                    Cid = item.Cid,
                    NumIid = item.NumIid,
                    OuterId = item.OuterId,
                    //替换原来的产品标题
                    Title =
                        item.Title.Replace(
                            SysConst.OriginalTitle,
                            SysConst.NewTitle)
                };
                req.GetProductSku(product, new RequestModel
                {
                    GoodsSn = item.OuterId,
                    Referer = goodsUrl
                });

                if (SysConst.IsModifyMainPic)
                {
                    #region old

                    /* old //todo 些处可以修改了，因为能获取到主图URL了
                     Bitmap watermark = imageWatermark.CreateWatermark((Bitmap)imageWatermark.SetByteToImage(SysUtils.GetImgByte(item.PicUrl)),
                                                                (Bitmap)
                                                                imageWatermark.SetByteToImage(
                                                                    SysUtils.GetImgByte(SysConst.ImgWatermark)),
                                                                ImageWatermark.WatermarkPosition.RightTop,
                                                                3);*/
                    #endregion

                    Bitmap watermark = SetTextAndIconWatermark(product.ThumbUrl, true);
                    product.Image = new FileItem("aa.jpg", imageWatermark.SetBitmapToBytes(watermark, ImageFormat.Jpeg));
                }

                #region 如果没有强制更新者 判断邦购数据是否以淘宝现在的库存数量一样，如果一样就取消更新

                if (!SysConst.IsEnforceUpdate)
                {
                    if (item.Num == product.ColorList.Sum(p => p.AvlNumForColor))
                    {
                        this._log.LogInfo(Resource.Log_StockEqualNotUpdate.StringFormat(item.NumIid, item.OuterId));
                        return;
                    }
                }

                #endregion

                this.DeleteAllSku(item);

                #region 如果是不修改价格（IsModifyPrice = false），则读取Item的price 填充到MySalePrice 中

                if (!isModifyPrice)
                {
                    foreach (var size in product.ColorList.SelectMany(color => color.SizeList))
                    {
                        size.MySalePrice = item.Price.ToType<double>();
                    }
                }

                #endregion

                this.UpdateGoodsAndUploadPic(product);
            }
            catch (Exception ex)
            {
                _log.LogError(Resource.Log_UpdateGoodsFailure.StringFormat(item.NumIid, item.OuterId), ex);
            }
        }
         
        //更新商品并上传相应的销售图片
        private void UpdateGoodsAndUploadPic(Product product)
        {
            UpdateGoodsInfo(product);

            foreach (ProductColor pColor in product.ColorList)
            {
                if (product.NumIid != null)
                    UploadItemPropimg(product.NumIid.Value, pColor.MapProps, new Uri(pColor.ImgUrl));

                Thread.Sleep(500);
            }
        }

        #endregion
          
        #region QueryGoods
         
        /// <summary>
        /// 按指定条件下架该产品
        /// </summary>
        /// <param name="lstSearch"></param>
        public void GoodsDelistingFromOnSale(IEnumerable<string> lstSearch)
        {
            foreach (var search in lstSearch)
            {
                if (search.IsEmptyString())
                {
                    continue;
                }

                List<Item> lstItem = GetOnSaleGoods(search);

                foreach (var item in lstItem)
                {
                    Thread.Sleep(200);

                    GoodsDelisting(item.NumIid);
                }
            } 
        }

        /// <summary>
        ///     得到单个商品信息
        ///     taobao.item.get
        /// </summary>
        /// <param name="req"></param>
        /// <returns>商品详情</returns>
        public Item GetGoods(ItemGetRequest req)
        {
            req.ThrowIfNull(Resource.ExceptionTemplate_MethedParameterIsNullorEmpty.StringFormat(new StackTrace()));

            var tContext = InstanceLocator.Current.GetInstance<TopContext>();

            ItemGetResponse response = _client.Execute(req, tContext.SessionKey);

            if (response.IsError)
                throw new TopResponseException(response.ErrCode, response.ErrMsg, response.SubErrCode,
                                               response.SubErrMsg, response.TopForbiddenFields);

            return response.Item;
        }

        /// <summary>
        ///     通过商品编号得到常用的Item数据
        ///     调用的GetGoods(ItemGetRequest req)
        /// </summary>
        /// <param name="numId">商品编号</param>
        /// <returns>商品详情</returns>
        public Item GetGoods(string numId)
        {
            numId.ThrowIfNullOrEmpty(
                Resource.ExceptionTemplate_MethedParameterIsNullorEmpty.StringFormat(new StackTrace()));

            var req = new ItemGetRequest
            {
                Fields =
                    "num_iid,title,nick,outer_id,price,num,location,post_fee,express_fee,ems_fee,sku,props_name,props,input_pids,input_str,pic_url,property_alias,item_weight,item_size,created,has_showcase,item_img,prop_img,desc",
                NumIid = numId.ToType<Int64>()
            };

            return GetGoods(req);
        }

        /// <summary>
        ///     得到产品列表
        /// </summary>
        /// <param name="numIds">各ID以","号分割</param>
        /// <returns></returns>
        public List<Item> GetGoodsList(string numIds)
        {
            var tContext = InstanceLocator.Current.GetInstance<TopContext>();

            var req = new ItemsListGetRequest { Fields = "num_iid,cid,num,sku,title,price,outer_id", NumIids = numIds };

            ItemsListGetResponse response = _client.Execute(req, tContext.SessionKey);

            if (response.IsError)
            {
                var ex = new TopResponseException(response.ErrCode, response.ErrMsg, response.SubErrCode,
                                                  response.SubErrMsg, response.TopForbiddenFields);
                _log.LogError(Resource.Log_GetGoodsListFailure, ex);
                throw ex;
            }

            return response.Items;
        }

        /// <summary>
        ///     获取当前会话用户出售中的商品列表
        ///     taobao.items.onsale.get
        /// </summary>
        /// <param name="req">要查询传入的参数</param>
        public List<Item> GetOnSaleGoods(ItemsOnsaleGetRequest req)
        {
            _log.LogInfo(Resource.Log_GetOnSaleGoodsing.StringFormat(req.Q));
            var tContext = InstanceLocator.Current.GetInstance<TopContext>();

            ItemsOnsaleGetResponse response = _client.Execute(req, tContext.SessionKey);

            if (response.IsError)
            {
                var ex = new TopResponseException(response.ErrCode, response.ErrMsg, response.SubErrCode,
                                                  response.SubErrMsg, response.TopForbiddenFields);

                _log.LogError(Resource.Log_GetOnSaleGoodsFailure, ex);

                throw ex;
            }

            _log.LogInfo(Resource.Log_GetOnSaleGoodsSuccess.StringFormat(req.Q));
            return response.Items;
        }

        /// <summary>
        /// 得到当前在售商品，最多个数为199
        /// </summary>
        /// <param name="search">查询条件</param>
        /// <returns></returns>
        public List<Item> GetOnSaleGoods(string search = null)
        {
            //得到当前用户的在售商品列表 
            var req = new ItemsOnsaleGetRequest { Fields = "num_iid,num,cid,title,outer_id,price,pic_url", PageSize = 199 };
            if (!search.IsNullOrEmpty())
                req.Q = search;

            return GetOnSaleGoods(req);
        }

        //得到卖家仓库中的商品
        public List<Item> GetInventoryGoods(ItemsInventoryGetRequest req)
        {
            var tContext = InstanceLocator.Current.GetInstance<TopContext>();

            ItemsInventoryGetResponse response = _client.Execute(req, tContext.SessionKey);

            if (response.IsError)
            {
                var ex = new TopResponseException(response.ErrCode, response.ErrMsg, response.SubErrCode,
                                                  response.SubErrMsg, response.TopForbiddenFields);

                _log.LogError(Resource.Log_GetInventoryGoodsFailure, ex);

                throw ex;
            }

            return response.Items;
        }
         
        /// <summary>
        ///     根据商品ID列表获取SKU信息
        /// taobao.item.skus.get 根据商品ID列表获取SKU信息 
        /// </summary>
        /// <param name="numIds">支持多个商品，用“，”号分割</param>
        /// <returns></returns>
        public IEnumerable<Sku> GetSkusByNumId(string numIds)
        {
            _log.LogInfo(Resource.Log_GetSkusing.StringFormat(numIds));
            var tContext = InstanceLocator.Current.GetInstance<TopContext>();

            var req = new ItemSkusGetRequest
            {
                Fields = "properties_name,sku_id,iid,num_iid,properties,quantity,price,outer_id",
                NumIids = numIds
            };

            ItemSkusGetResponse response = _client.Execute(req, tContext.SessionKey);

            if (response.IsError)
            {
                var ex = new TopResponseException(response.ErrCode, response.ErrMsg, response.SubErrCode,
                                                  response.SubErrMsg, response.TopForbiddenFields);
                _log.LogError(Resource.Log_GetSkusFailure.StringFormat(numIds), ex);
                throw ex;
            }
            _log.LogInfo(Resource.Log_GetSkusSuccess.StringFormat(numIds));

            return response.Skus.OrderBy(f => f.SkuId).ToList();
        }


        #endregion

        #region DeleteGoods
         
        /// <summary>
        ///     删除单个SKU
        /// taobao.item.sku.delete 删除SKU 
        /// </summary>
        /// <param name="numId"></param>
        /// <param name="properties"></param>
        public void DeleteGoodsSku(long numId, string properties, string goodsSn = "")
        {
            _log.LogInfo(Resource.Log_DeleteGoodsSkuing.StringFormat(numId, properties, goodsSn));
            var tContext = InstanceLocator.Current.GetInstance<TopContext>();
            var req = new ItemSkuDeleteRequest { NumIid = numId, Properties = properties };

            ItemSkuDeleteResponse response = _client.Execute(req, tContext.SessionKey);

            if (response.IsError)
            {
                var ex = new TopResponseException(response.ErrCode, response.ErrMsg, response.SubErrCode,
                                                  response.SubErrMsg, response.TopForbiddenFields);

                _log.LogError(Resource.Log_DeleteGoodsSkuFailure.StringFormat(numId, properties, goodsSn), ex);
            }
            _log.LogInfo(Resource.Log_DeleteGoodsSkuSuccess.StringFormat(numId, properties, goodsSn));
        }
         
        /// <summary>
        /// 删除该商品的销售图片
        /// taobao.item.propimg.delete 删除属性图片
        /// </summary>
        /// <param name="imgId">图片ID</param>
        /// <param name="numId">商品编号</param>
        /// <returns></returns>
        public PropImg DeleteItemPropimg(long imgId, long numId, string goodsSn = "")
        {
            _log.LogInfo(Resource.Log_DeleteItemPropingimg, imgId, numId, goodsSn);

            var req = new ItemPropimgDeleteRequest { NumIid = numId, Id = imgId };
            var tContext = InstanceLocator.Current.GetInstance<TopContext>();
            var response = _client.Execute(req, tContext.SessionKey);

            if (response.IsError)
            {
                var ex = new TopResponseException(response.ErrCode, response.ErrMsg, response.SubErrCode,
                                                  response.SubErrMsg, response.TopForbiddenFields);

                _log.LogError(Resource.Log_DeleteItemPropimgFailure.StringFormat(imgId, numId, goodsSn), ex);
            }

            _log.LogInfo(Resource.Log_DeleteItemPropimgSuccess, imgId, numId, goodsSn);

            return response.PropImg;
        }

        #endregion

        #region Common Method

        /// <summary>
        ///     更新和添加销售商品图片
        /// taobao.item.propimg.upload 添加或修改属性图片 
        /// </summary>
        /// <param name="numId">商品编号</param>
        /// <param name="properties">销售属性</param>
        /// <param name="urlImg">网上的图片地址</param>
        /// <returns></returns>
        public PropImg UploadItemPropimg(long numId, string properties, Uri urlImg)
        {
            int len = urlImg.Segments.Length;
            string fileName = len > 0
                                  ? urlImg.Segments[len - 1]
                                  : "{0}-{1}.jpg".StringFormat(numId.ToString(CultureInfo.InvariantCulture), properties);

          /*  Bitmap watermark = imageWatermark.CreateWatermark(
               (Bitmap)imageWatermark.SetByteToImage(SysUtils.GetImgByte(urlImg.ToString())),
               SysConst.TextWatermark,
               ImageWatermark.WatermarkPosition.RigthBottom,
               3);*/
            Bitmap watermark = SetTextAndIconWatermark(urlImg.ToString(), false);

            var fItem = new FileItem(fileName, imageWatermark.SetBitmapToBytes(watermark, ImageFormat.Jpeg));

//            var fItem = new FileItem(fileName, SysUtils.GetImgByte(urlImg.ToString()));
            return UploadItemPropimgInternal(numId, properties, fItem);
        }

        /// <summary>
        /// 检查该产品是否已经上架
        /// taobao.items.onsale.get 获取当前会话用户出售中的商品列表
        /// </summary>
        /// <param name="goodsSn">款号</param>
        /// <returns></returns>
        public Item VerifyGoodsExist(string goodsSn)
        {
            goodsSn.ThrowIfNullOrEmpty(
                Resource.ExceptionTemplate_MethedParameterIsNullorEmpty.StringFormat(new StackTrace()));

            var req = new ItemsOnsaleGetRequest { Fields = "num_iid,num,cid,title,outer_id,price,pic_url", Q = goodsSn, PageSize = 10 };
            List<Item> onSaleGoods = GetOnSaleGoods(req);

            if (onSaleGoods != null && onSaleGoods.Count > 0)
            {
                _log.LogWarning(Resource.Log_GoodsAlreadyExist, goodsSn);
                return onSaleGoods[0];
            }

            return null;
        }

        /// <summary>
        ///     更新和添加销售商品图片
        /// taobao.item.propimg.upload 添加或修改属性图片 
        /// </summary>
        /// <param name="numId">商品编号</param>
        /// <param name="properties">销售属性</param>
        /// <param name="imgPath">本地图片路径</param>
        /// <returns></returns>
        public PropImg UploadItemPropimg(long numId, string properties, string imgPath)
        {
            #region validation

            if (numId <= 0 || string.IsNullOrEmpty(properties) || string.IsNullOrEmpty(imgPath))
                throw new Exception((Resource.ExceptionTemplate_MethedParameterIsNullorEmpty.StringFormat(
                    new StackTrace())));

            #endregion 

             Bitmap watermark = imageWatermark.CreateWatermark(
               (Bitmap)imageWatermark.SetByteToImage(FileHelper.ReadFile(imgPath)),
               SysConst.TextWatermark,
               ImageWatermark.WatermarkPosition.RigthBottom,
               3);

            var fItem = new FileItem("{0}-{1}.jpg".StringFormat(numId.ToString(CultureInfo.InvariantCulture), properties),imageWatermark.SetBitmapToBytes(watermark,ImageFormat.Jpeg));

            return UploadItemPropimgInternal(numId, properties, fItem);
        }
         
        /// <summary>
        ///     taobao.item.update.delisting 商品下架
        /// </summary>
        /// <param name="numId">商品编号</param>
        /// <returns></returns>
        public Item GoodsDelisting(long numId)
        {
            _log.LogInfo(Resource.Log_GoodsDelisting, numId);

            var req = new ItemUpdateDelistingRequest { NumIid = numId };
            var tContext = InstanceLocator.Current.GetInstance<TopContext>();
            ItemUpdateDelistingResponse response = _client.Execute(req, tContext.SessionKey);

            if (response.IsError)
            {
                var ex = new TopResponseException(response.ErrCode, response.ErrMsg, response.SubErrCode,
                                                  response.SubErrMsg, response.TopForbiddenFields);

                _log.LogError(Resource.Log_GoodsDelistingFailure.StringFormat(numId), ex);
            }

            _log.LogInfo(Resource.Log_GoodsDelistingSuccess, numId);

            return response.Item;
        }
         
        #endregion
 
        #endregion

        #region Private Methods
         
        //现在淘宝可以将SKU全部删除完,并将该产品对应的销售图片给删除掉 
        private void DeleteAllSku(Item item)
        {
            if (item.Skus.Count == 0 || item.Skus[0].Properties.IsNullOrEmpty())
            {
                //因为读在售没办法得到SKU所以只有单独取 
                try
                {
                    item.Skus = (List<Sku>)GetSkusByNumId(item.NumIid.ToString(CultureInfo.InvariantCulture));
                    Thread.Sleep(200);
                }
                // ReSharper disable EmptyGeneralCatchClause
                catch
                // ReSharper restore EmptyGeneralCatchClause
                {
                }
            }

            //不用排序，默认取最开始那个 
            List<Sku> skus = item.Skus;

            if (skus == null)
                return;

            var zeroSkus = skus.Where(s => s.Quantity == 0);

            foreach (var zeroSku in zeroSkus)
            {
                //如果数量为0，删除SKU会报错
                UpdateSku(new ItemSkuUpdateRequest()
                {
                    NumIid = item.NumIid,
                    Properties = zeroSku.Properties,
                    Quantity = 1,
                    OuterId = item.OuterId
                });
                Thread.Sleep(200);
            }

             
            //现在淘宝可以把所有SKU全部删除完了
            foreach (Sku sku in skus)
            { 
                this.DeleteGoodsSku(item.NumIid, sku.Properties, item.OuterId);
                  
                Thread.Sleep(500);
            }

            #region 删除商品的销售图片

            var goods= GetGoods(item.NumIid.ToString(CultureInfo.InvariantCulture));

            foreach (var propImg in goods.PropImgs)
            {
                DeleteItemPropimg(propImg.Id, item.NumIid,item.OuterId);
                Thread.Sleep(100);
            }
             
            #endregion
        }
         
        //重EXCEL中读取要发布的数据用于发布或更新商品SKU
        private static IEnumerable<PublishGoods> GetPublishGoodsFromExcel(string filePath)
        {
            filePath.ThrowIfNullOrEmpty(
                Resource.ExceptionTemplate_MethedParameterIsNullorEmpty.StringFormat(new StackTrace()));

            DataTable dtSource = ExcelHelper.GetExcelData(filePath, Resource.SysConfig_Sku);

            dtSource.ThrowIfNull(Resource.ExceptionTemplate_MethedParameterIsNullorEmpty.StringFormat(new StackTrace()));
            return (from DataRow dr in dtSource.Rows select new PublishGoods(dr)).ToList();
        }

        //填充产品信息，将banggo的数据填充进相应的请求模型中
        private void StuffProductInfo(Product bProduct)
        {
            _log.LogInfo(Resource.Log_StuffProductInfoing.StringFormat(bProduct.GoodsSn));
            bProduct.OuterId = bProduct.GoodsSn;
             
//            _banggoMgt.SetCidAndSellerCids(bProduct);

            var watermark = SetTextAndIconWatermark(bProduct.ThumbUrl,true);

            bProduct.Image = new FileItem(bProduct.GoodsSn + ".jpg", imageWatermark.SetBitmapToBytes(watermark,ImageFormat.Jpeg));

            //bProduct.Image = new FileItem(bProduct.GoodsSn + ".jpg", SysUtils.GetImgByte(bProduct.ThumbUrl));

          

            //得到运费模版
            string deliveryTemplateId = _delivery.GetDeliveryTemplateId(Resource.SysConfig_DeliveryTemplateName);

            if (deliveryTemplateId == null)
            {
                SetDeliveryFee(bProduct);
            }
            else
            {
                bProduct.PostageId = deliveryTemplateId.ToType<Int64>();
                bProduct.ItemWeight = Resource.SysConfig_ItemWeight;
            }

            string itemProps = _catalog.GetItemProps(bProduct.Cid.ToString());
            bProduct.Props = itemProps; //只先提取必填项

            SetOptionalProps(bProduct);
            
            SetSkuInfo(bProduct);

            watermark.Dispose();

            _log.LogInfo(Resource.Log_StuffProductInfoSuccess.StringFormat(bProduct.GoodsSn));
        }

        //添加文字水印和图片水印
        private Bitmap SetTextAndIconWatermark(string picUrl,bool isMainPic)
        {
            Bitmap watermark = imageWatermark.CreateWatermark(
                (Bitmap)imageWatermark.SetByteToImage(SysUtils.GetImgByte(picUrl)),
                SysConst.TextWatermark,
                ImageWatermark.WatermarkPosition.RigthBottom,
                3);

            if (isMainPic)//只有该图片地址是主图才会去给主图加图片水印
            {
                if (SysConst.IsModifyMainPic)
                {
                    watermark = imageWatermark.CreateWatermark(watermark,
                                                               (Bitmap)
                                                               imageWatermark.SetByteToImage(
                                                                   SysUtils.GetImgByte(SysConst.ImgWatermark)),
                                                               ImageWatermark.WatermarkPosition.RightTop,
                                                               3);
                }
            }
            return watermark;
        }

        //包括设置品牌、货号
        private void SetOptionalProps(Product bProduct)
        {
            //取消在Props中增加品牌，因为在更新SKU没办法得到Brand
            /* var rm = new ResourceManager(typeof(Resource).FullName,
                                         typeof(Resource).Assembly);
            string brandProp = rm.GetString("SysConfig_{0}_BrandProp".StringFormat(bProduct.Brand));

            if (!brandProp.IsNullOrEmpty())
            {
                bProduct.Props += brandProp;
            }*/

            bProduct.InputPids = "{0},{1}".StringFormat(Resource.SysConfig_ProductCodeProp, "20000");
            bProduct.InputStr = "{0},{1}".StringFormat(bProduct.GoodsSn, bProduct.Brand);
        }

        //taobao.item.propimg.upload 添加或修改属性图片 
        private PropImg UploadItemPropimgInternal(long numId, string properties, FileItem fItem)
        {
            _log.LogInfo(Resource.Log_PublishSaleImging, numId);

            var req = new ItemPropimgUploadRequest {NumIid = numId, Image = fItem, Properties = properties};

            var tContext = InstanceLocator.Current.GetInstance<TopContext>();
            ItemPropimgUploadResponse response = _client.Execute(req, tContext.SessionKey);

            if (response.IsError)
            {
                var ex = new TopResponseException(response.ErrCode, response.ErrMsg, response.SubErrCode,
                                                  response.SubErrMsg, response.TopForbiddenFields);
                _log.LogError(Resource.Log_PublishSaleImgFailure, ex);
                throw ex;
            }

            _log.LogInfo(Resource.Log_PublishSaleImgSuccess, response.PropImg.Id, response.PropImg.Url);
            return response.PropImg;
        }

        private void SetSkuInfo(Product bProduct)
        {
            #region var

            var sbSku = new StringBuilder();
            var sbSkuToProps = new StringBuilder();
            var lstSkuAlias = new List<string>();
            var lstSkuQuantities = new List<string>();
            var lstSkuPrices = new List<string>();
            var sbSkuOuterIds = new StringBuilder();

            int num = 0; //商品数量

            #endregion

            List<string> propColors = _catalog.GetSaleProp(true, bProduct.Cid.ToString());

            List<string> propSize = _catalog.GetSaleProp(false, bProduct.Cid.ToString());
            int colorCount = bProduct.ColorList.Count;

            //清空现有的色码与淘宝的属性映射
            _dicColorMap.Clear();

            List<string> keys = bProduct.BSizeToTSize.Keys.ToList();
            bProduct.BSizeToTSize.Clear();
            for (int k = 0; k < keys.Count; k++)
            {
                bProduct.BSizeToTSize.Add(keys[k], propSize[k]);
            }

            for (int i = 0; i < colorCount; i++)
            {
                string pColor = propColors[i];

                ProductColor bColor = bProduct.ColorList[i];

                bColor.MapProps = pColor;

                _dicColorMap.Add(bColor.ColorCode, pColor);

                num += bColor.AvlNumForColor;

                sbSkuToProps.AppendFormat("{0}{1}", pColor, CommomConst.SEMI);
                lstSkuAlias.Add("{0}{1}{2}({3}色){4}".StringFormat(pColor, CommomConst.COLON, bColor.Title,
                                                                  bColor.ColorCode,
                                                                  CommomConst.SEMI));

                //读取尺码
                int sizeCount = bColor.SizeList.Count;
                for (int j = 0; j < sizeCount; j++)
                {
                    #region 构造尺码

                    ProductSize bSize = bColor.SizeList[j];
                    string pSize;
                    if (!bProduct.BSizeToTSize.TryGetValue(bSize.Alias, out pSize))
                        continue;

                    sbSku.AppendFormat("{0}{1}", pColor, CommomConst.SEMI);
                    sbSku.AppendFormat("{0}{1}", pSize, CommomConst.COMMA);

                    if (sbSkuOuterIds.Length > 0)
                    {
                        sbSkuOuterIds.AppendFormat(",{0}", bProduct.GoodsSn);
                    }
                    else
                    {
                        sbSkuOuterIds.Append(bProduct.GoodsSn);
                    }

                    sbSkuToProps.AppendFormat("{0}{1}", pSize, CommomConst.SEMI);

                    // 不用为每个尺码都加别名
                    if (lstSkuAlias.Find(a => a.Contains(pSize)) == null)
                        lstSkuAlias.Add("{0}{1}{2}({3}){4}".StringFormat(pSize, CommomConst.COLON, bSize.Alias,
                                                                         bSize.SizeCode, CommomConst.SEMI));

                    lstSkuQuantities.Add(bSize.AvlNum.ToString(CultureInfo.InvariantCulture));
                    //num += bSize.AvlNum; 通过在颜色中去读取有效库存

                    if (bProduct.Price.IsNullOrEmpty())
                        bProduct.Price = bSize.MySalePrice.ToString(CultureInfo.InvariantCulture);

                    lstSkuPrices.Add(bSize.MySalePrice.ToString(CultureInfo.InvariantCulture));

                    #endregion
                }
            }

            bProduct.Num = num;

            bProduct.Props += sbSkuToProps.ToString();
            bProduct.PropertyAlias = lstSkuAlias.ToColumnString("");
            bProduct.SkuProperties = sbSku.ToString().TrimEnd(',');

            bProduct.SkuQuantities = lstSkuQuantities.ToColumnString();
            bProduct.SkuPrices = lstSkuPrices.ToColumnString();
            bProduct.SkuOuterIds = sbSkuOuterIds.ToString();
        }

        private static void SetDeliveryFee(Product tProduct)
        {
            tProduct.PostFee = Resource.SysConfig_PostFee;
            tProduct.ExpressFee = Resource.SysConfig_ExpressFee;
            tProduct.EmsFee = Resource.SysConfig_EmsFee;
        }

        #endregion
         
    }
}