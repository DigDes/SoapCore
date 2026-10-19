using System.ServiceModel;

namespace SoapCore.Tests.Ui
{
	// Contracts bigger than the explorer lists: 105 parameters and 22 different declared faults. The tests register the interface itself.
	[ServiceContract(Namespace = "urn:ui-limits")]
	public interface IUiLimitsService
	{
		[OperationContract]
		string Many(int p001, int p002, int p003, int p004, int p005, int p006, int p007, int p008, int p009, int p010, int p011, int p012, int p013, int p014, int p015, int p016, int p017, int p018, int p019, int p020, int p021, int p022, int p023, int p024, int p025, int p026, int p027, int p028, int p029, int p030, int p031, int p032, int p033, int p034, int p035, int p036, int p037, int p038, int p039, int p040, int p041, int p042, int p043, int p044, int p045, int p046, int p047, int p048, int p049, int p050, int p051, int p052, int p053, int p054, int p055, int p056, int p057, int p058, int p059, int p060, int p061, int p062, int p063, int p064, int p065, int p066, int p067, int p068, int p069, int p070, int p071, int p072, int p073, int p074, int p075, int p076, int p077, int p078, int p079, int p080, int p081, int p082, int p083, int p084, int p085, int p086, int p087, int p088, int p089, int p090, int p091, int p092, int p093, int p094, int p095, int p096, int p097, int p098, int p099, int p100, int p101, int p102, int p103, int p104, int p105);

		[OperationContract]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<bool>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<byte>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<sbyte>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<short>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<ushort>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<int>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<uint>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<long>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<ulong>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<float>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<double>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<decimal>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<char>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<string>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<object>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<System.Guid>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<System.DateTime>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<System.TimeSpan>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<System.DateTimeOffset>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<System.Uri>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<System.Version>))]
		[FaultContract(typeof(UiDetailsModels.KeyedFault<System.Exception>))]
		string Faulty();
	}
}
