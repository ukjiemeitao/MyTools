﻿///////////////////////////////////////////////////////////
//  Product.cs
//  Implementation of the Class Product
//  Generated by Enterprise Architect
//  Created on:      05-五月-2013 16:32:54
//  Original author: 吉桂昕
///////////////////////////////////////////////////////////

using System;
using Top.Api.Util;

namespace MyTools.TaoBao.DomainModule
{

    /// <summary>
    /// 类目API taobao.itemprops.get 获取标准商品类目属性（包括：货号、品牌、衣长等）
    /// 注：1，货号，是放在input_str，input_pids
    /// 中，如：【（input_pids：input_str）（1632501：238286）】2，品牌，如果淘宝类目中有该品牌如：那么就加到props中，如果没有，需
    /// 要自定义品牌者加到input_str，input_pids，如：【（input_pids：input_str）（20000：莱克）】3,
    /// SUK（销售）属性，sku_properties中有的属性props也必须存在。sku_properties中以“，”分开。
    /// </summary>
    public class Product
    {

        /// <summary>
        ///叶子类目id  类目API---- taobao.itemcats.get
        /// </summary>
        public Nullable<long> Cid { get; set; }

        /// <summary>
        /// 宝贝描述。字数要大于5个字符，小于25000个字符，受违禁词控制
        /// </summary>
        public string Desc { get; set; }

        /// <summary>
        /// 运费承担方式。可选值:seller（卖家承担）,buyer(买家承担);默认值:seller。卖家承担不用设置邮费和postage_id.
        /// 买家承担的时候，必填邮费和postage_id 如果用户设置了运费模板会优先使用运费模板，否则要同步设置邮费（post_fee,express_fee,
        /// ems_fee）
        /// </summary>
        public string FreightPayer = "buyer";

        /// <summary>
        /// 橱窗推荐。可选值:true,false;默认值:false(不推荐)
        /// </summary>
        public Nullable<bool> HasShowcase { get; set; }

        /// <summary>
        /// 商品主图片。类型:JPG,GIF;最大长度:500K
        /// 支持的文件类型：gif,jpg,jpeg,png
        /// </summary>
        public FileItem Image { get; set; }

        /// <summary>
        /// 结构："pid1,pid2,pid3"，如："20000"（表示品牌） 注：通常一个类目下用户可输入的关键属性不超过1个。
        /// </summary>
        public string InputPids { get; set; }

        /// <summary>
        /// 用户自行输入的子属性名和属性值，结构:"父属性值;一级子属性名;一级子属性值;二级子属性名;自定义输入值,....",如：“耐克;耐克系列;科比系列;科比系列;2K5,Nike乔丹鞋;乔丹系列;乔丹鞋系列;乔丹鞋系列;json5”，多个自定义属性用','分割，input_str需要与input_pids一一对应，注：通常一个类目下用户可输入的关键属性不超过1个。所有属性别名加起来不能超过3999字节
        /// </summary>
        public string InputStr { get; set; }

        /// <summary>
        /// 所在地城市。如杭州 。可以通过http://dl.open.taobao.com/sdk/商品城市列表.rar查询
        /// </summary>
        public string LocationCity { get; set; }
        /// <summary>
        /// 所在地省份。如浙江，具体可以下载http://dl.open.taobao.com/sdk/商品城市列表.rar  取到
        /// </summary>
        public string LocationState { get; set; }

        /// <summary>
        /// 商品数量，取值范围:0-999999的整数。且需要等于Sku所有数量的和。  拍卖商品中增加拍只能为1，荷兰拍要在[2,500)范围内。
        /// </summary>
        public Nullable<long> Num { get; set; }

        /// <summary>
        /// 商品外部编码，该字段的最大长度是512个字节
        /// </summary>
        public string OuterId { get; set; }

        /// <summary>
        /// 宝贝所属的运费模板ID。取值范围：整数且必须是该卖家的运费模板的ID（可通过taobao.delivery.template.get获得当前会话用户的所有邮费模板）
        /// </summary>
        public Nullable<long> PostageId { get; set; }

        /// <summary>
        /// 商品价格。取值范围:0-100000000;精确到2位小数;单位:元。如:200.07，表示:200元7分。需要在正确的价格区间内。 拍卖商品对应的起拍价。
        /// </summary>
        public string Price { get; set; }

        /// <summary>
        /// 如pid:vid:别名;pid1:vid1:别名1 ，其中：pid是属性id vid是属性值id。总长度不超过511字节
        /// </summary>
        public string PropertyAlias { get; set; }

        /// <summary>
        /// 商品属性列表。格式:pid:vid;pid:vid。属性的pid调用taobao.itemprops.get取得，属性值的vid用taobao.
        /// itempropvalues.get取得vid。 如果该类目下面没有属性，可以不用填写。如果有属性，必选属性必填，其他非必选属性可以选择不填写.
        /// 属性不能超过35对。所有属性加起来包括分割符不能超过549字节，单个属性没有限制。 如果有属性是可输入的话，则用字段input_str填入属性的值
        /// </summary>
        public string Props { get; set; }

        /// <summary>
        /// 按逗号分隔。结构:",cid1,cid2,...,"，如果店铺类目存在二级类目，必须传入子类目cids。
        /// </summary>
        public string SellerCids { get; set; }

        /// <summary>
        /// Sku的外部id串，结构如：1234,1342,…  sku_properties, sku_quantities, sku_prices, sku_outer_ids在输入数据时要一一对应，如果没有sku_outer_ids也要写上这个参数，入参是","(这个是两个sku的示列，逗号数应该是sku个数减1)；该参数最大长度是512个字节
        /// </summary>
        public string SkuOuterIds { get; set; }

        /// <summary>
        /// 结构如：10.00,5.00,… 精确到2位小数;单位:元。如:200.07，表示:200元7分
        /// </summary>
        public string SkuPrices { get; set; }

        /// <summary>
        /// 更新的Sku的属性串，调用taobao.itemprops.get获取类目属性，如果属性是销售属性，再用taobao.itempropvalues.
        /// get取得vid。格式:pid:vid;pid:vid,
        /// 多个sku之间用逗号分隔。该字段内的销售属性（自定义的除外）也需要在props字段填写。sku的销售属性需要一同选取，如:
        /// 颜色，尺寸。如果新增商品包含了sku，则此字段一定要传入。这个字段的长度要控制在512个字节以内。 如果有自定义销售属性，则格式为pid:vid;pid2:
        /// vid2;$pText:vText , 其中$pText:
        /// vText为自定义属性。限制：其中$pText的’$’前缀不能少，且pText和vText文本中不可以存在冒号:和分号;以及逗号，
        /// </summary>
        public string SkuProperties { get; set; }

        /// <summary>
        /// 结构如：num1,num2,num3 如：2,3
        /// </summary>
        public string SkuQuantities { get; set; }

        /// <summary>
        /// 新旧程度。可选值：new(新)，second(二手)，unused(闲置)。B商家不能发布二手商品。 如果是二手商品，特定类目下属性里面必填新旧成色属性
        /// </summary>
        public string StuffStatus = "new";

        /// <summary>
        /// 宝贝标题。不能超过60字符，受违禁词控制
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 发布类型。可选值:fixed(一口价),auction(拍卖)。B商家不能发布拍卖商品，而且拍卖商品是没有SKU的。
        /// 拍卖商品发布时需要附加拍卖商品信息：拍卖类型(paimai_info.
        /// mode，拍卖类型包括三种：增价拍[1]，荷兰拍[2]以及降价拍[3])，商品数量(num)，起拍价(price)，价格幅度(increament)，保证金(p
        /// aimai_info.deposit)。另外拍卖商品支持自定义销售周期，通过paimai_info.valid_hour和paimai_info.
        /// valid_minute来指定。对于降价拍来说需要设置降价周期(paimai_info.interval)和拍卖保留价(paimai_info.
        /// reserve)。 注意：通过taobao.item.get接口获取拍卖信息时，会返回除了valid_hour和valid_minute之外的所有拍卖信息
        /// </summary>
        public string Type = "fixed";

        public virtual void Dispose()
        {

        }

    }

//end Product
}
