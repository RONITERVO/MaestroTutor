// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type {RoomCaptureImage} from '../../../shared/roomViewCapture';
export function RoomCapturePreview({image}:{image:RoomCaptureImage}) {
 return <figure className="my-3"><img src={`data:${image.capture.mimeType};base64,${image.data}`} width={image.capture.width} height={image.capture.height} alt="Virtual room snapshot" style={{maxWidth:'100%',height:'auto'}}/><figcaption>Virtual objects only · {new Date(image.capture.capturedAt).toLocaleString()}. The book, tool trays and real room are excluded.</figcaption></figure>;
}
