using System;
using System.Collections.Generic;
using System.Linq;

namespace OfferwallApi.Infrastructure.Results;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }
    public List<Error> Errors { get; } = new();

    protected Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
        if (!isSuccess && error != Error.None)
        {
            Errors.Add(error);
        }
    }

    protected Result(List<Error> errors)
    {
        IsSuccess = false;
        Error = errors.FirstOrDefault() ?? Error.None;
        Errors = errors;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result Failure(List<Error> errors) => new(errors);

    public static implicit operator Result(Error error) => Failure(error);
    public static implicit operator Result(List<Error> errors) => Failure(errors);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error)
    {
        _value = value;
    }

    protected Result(List<Error> errors) : base(errors)
    {
        _value = default;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failure result cannot be accessed.");

    public static Result<TValue> Success(TValue value) => new(value, true, Error.None);
    public new static Result<TValue> Failure(Error error) => new(default, false, error);
    public new static Result<TValue> Failure(List<Error> errors) => new(errors);

    public static implicit operator Result<TValue>(TValue value) => Success(value);
    public static implicit operator Result<TValue>(Error error) => Failure(error);
    public static implicit operator Result<TValue>(List<Error> errors) => Failure(errors);
}
