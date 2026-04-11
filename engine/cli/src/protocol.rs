//! MessagePack protocol: length-prefixed framing over stdin/stdout.
//!
//! Wire format: [4-byte big-endian length][MessagePack payload]
//! Both requests (stdin) and responses (stdout) use this framing.

use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::io::{self, Read, Write};

/// A protocol request from the client.
#[derive(Debug, Serialize, Deserialize)]
pub struct Request {
    pub method: String,
    #[serde(default)]
    pub params: serde_json::Value,
}

/// A protocol response to the client.
#[derive(Debug, Serialize, Deserialize)]
pub struct Response {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub result: Option<serde_json::Value>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<ErrorInfo>,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct ErrorInfo {
    pub code: String,
    pub message: String,
}

/// Compile result: parameter schema + initial series + graph topology.
#[derive(Debug, Serialize)]
pub struct CompileResult {
    pub params: Vec<ParamInfo>,
    pub series: HashMap<String, Vec<f64>>,
    pub bins: usize,
    pub grid: GridInfo,
    pub graph: GraphInfoMsg,
}

/// Graph structure for UI visualization (nodes + edges).
#[derive(Debug, Serialize)]
pub struct GraphInfoMsg {
    pub nodes: Vec<GraphNodeMsg>,
    pub edges: Vec<GraphEdgeMsg>,
}

#[derive(Debug, Serialize)]
pub struct GraphNodeMsg {
    pub id: String,
    pub kind: String,
}

#[derive(Debug, Serialize)]
pub struct GraphEdgeMsg {
    pub from: String,
    pub to: String,
}

#[derive(Debug, Serialize)]
pub struct ParamInfo {
    pub id: String,
    pub kind: String,
    pub default: serde_json::Value,
}

#[derive(Debug, Serialize)]
pub struct GridInfo {
    pub bins: i32,
    #[serde(rename = "binSize")]
    pub bin_size: i32,
    #[serde(rename = "binUnit")]
    pub bin_unit: String,
}

/// Eval result: updated series + timing.
#[derive(Debug, Serialize, Deserialize)]
pub struct EvalResultMsg {
    pub series: HashMap<String, Vec<f64>>,
    pub elapsed_us: u64,
}

impl Response {
    pub fn ok(result: serde_json::Value) -> Self {
        Self { result: Some(result), error: None }
    }

    pub fn err(code: &str, message: &str) -> Self {
        Self {
            result: None,
            error: Some(ErrorInfo {
                code: code.to_string(),
                message: message.to_string(),
            }),
        }
    }
}

/// Read a length-prefixed MessagePack message from a reader.
/// Returns None on EOF.
pub fn read_message<R: Read>(reader: &mut R) -> io::Result<Option<Request>> {
    // Read 4-byte big-endian length
    let mut len_buf = [0u8; 4];
    match reader.read_exact(&mut len_buf) {
        Ok(()) => {}
        Err(e) if e.kind() == io::ErrorKind::UnexpectedEof => return Ok(None),
        Err(e) => return Err(e),
    }
    let len = u32::from_be_bytes(len_buf) as usize;

    if len == 0 {
        return Err(io::Error::new(io::ErrorKind::InvalidData, "zero-length message"));
    }
    if len > 64 * 1024 * 1024 {
        return Err(io::Error::new(io::ErrorKind::InvalidData, "message too large (>64MB)"));
    }

    // Read payload
    let mut buf = vec![0u8; len];
    reader.read_exact(&mut buf)?;

    // Deserialize MessagePack
    let req: Request = rmp_serde::from_slice(&buf)
        .map_err(|e| io::Error::new(io::ErrorKind::InvalidData, format!("MessagePack decode error: {e}")))?;

    Ok(Some(req))
}

/// Write a length-prefixed MessagePack message to a writer.
pub fn write_message<W: Write>(writer: &mut W, response: &Response) -> io::Result<()> {
    let payload = rmp_serde::to_vec_named(response)
        .map_err(|e| io::Error::new(io::ErrorKind::Other, format!("MessagePack encode error: {e}")))?;

    let len = payload.len() as u32;
    writer.write_all(&len.to_be_bytes())?;
    writer.write_all(&payload)?;
    writer.flush()?;

    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn round_trip_response() {
        let resp = Response::ok(serde_json::json!({ "hello": "world" }));
        let mut buf = Vec::new();
        write_message(&mut buf, &resp).unwrap();

        // Verify framing: 4-byte length prefix
        let len = u32::from_be_bytes([buf[0], buf[1], buf[2], buf[3]]) as usize;
        assert_eq!(len, buf.len() - 4);

        // Deserialize payload
        let payload: Response = rmp_serde::from_slice(&buf[4..]).unwrap();
        assert!(payload.result.is_some());
        assert_eq!(payload.result.unwrap()["hello"], "world");
    }

    #[test]
    fn round_trip_request() {
        let req = Request {
            method: "compile".to_string(),
            params: serde_json::json!({ "yaml": "test" }),
        };
        let payload = rmp_serde::to_vec_named(&req).unwrap();

        let mut buf = Vec::new();
        let len = payload.len() as u32;
        buf.extend_from_slice(&len.to_be_bytes());
        buf.extend_from_slice(&payload);

        let mut cursor = io::Cursor::new(buf);
        let decoded = read_message(&mut cursor).unwrap().unwrap();
        assert_eq!(decoded.method, "compile");
        assert_eq!(decoded.params["yaml"], "test");
    }

    #[test]
    fn read_message_eof_returns_none() {
        let mut cursor = io::Cursor::new(Vec::<u8>::new());
        let result = read_message(&mut cursor).unwrap();
        assert!(result.is_none());
    }

    #[test]
    fn error_response_serializes() {
        let resp = Response::err("not_compiled", "No model compiled yet");
        let mut buf = Vec::new();
        write_message(&mut buf, &resp).unwrap();

        let payload: Response = rmp_serde::from_slice(&buf[4..]).unwrap();
        assert!(payload.error.is_some());
        assert_eq!(payload.error.as_ref().unwrap().code, "not_compiled");
    }

    #[test]
    fn series_data_round_trips() {
        let series: HashMap<String, Vec<f64>> = [
            ("arrivals".to_string(), vec![10.0, 20.0, 30.0]),
            ("served".to_string(), vec![8.0, 16.0, 24.0]),
        ].into();

        let result = EvalResultMsg { series, elapsed_us: 42 };
        let resp = Response::ok(serde_json::to_value(&result).unwrap());

        let mut buf = Vec::new();
        write_message(&mut buf, &resp).unwrap();

        let payload: Response = rmp_serde::from_slice(&buf[4..]).unwrap();
        let inner: EvalResultMsg = serde_json::from_value(payload.result.unwrap()).unwrap();
        assert_eq!(inner.series["arrivals"], vec![10.0, 20.0, 30.0]);
        assert_eq!(inner.elapsed_us, 42);
    }
}
