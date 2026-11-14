/**
 * Dịch các thông báo lỗi kiểm tra luật UML sang tiếng Việt thân thiện với người dùng.
 */
export function translateUmlError(error: string): string {
  if (!error) return error;

  let msg = error.trim();

  // Bỏ tiền tố "uml: " hoặc "Diagram model violates UML rules: " nếu có
  msg = msg.replace(/^uml:\s*/i, '');
  msg = msg.replace(/^Diagram model violates UML rules:\s*/i, '');

  // Business Context
  if (msg.includes('BusinessContext diagram requires a title')) {
    return 'Sơ đồ Business Context yêu cầu phải có tiêu đề (title).';
  }
  if (msg.includes('BusinessContext diagram requires a primary system boundary name')) {
    return "Sơ đồ Business Context yêu cầu phải có tên hệ thống chính (thuộc tính 'system').";
  }
  if (msg.includes('BusinessContext diagram requires at least one external entity')) {
    return "Sơ đồ Business Context yêu cầu phải có ít nhất một thực thể bên ngoài (danh sách 'externalSystems').";
  }
  if (msg.includes('Flow source') && msg.includes('is neither the system')) {
    return msg
      .replace('Flow source', 'Nguồn luồng dữ liệu')
      .replace('is neither the system, an internal component nor an external entity.', 'không thuộc hệ thống, thành phần nội bộ hay thực thể bên ngoài.');
  }
  if (msg.includes('Flow target') && msg.includes('is neither the system')) {
    return msg
      .replace('Flow target', 'Đích luồng dữ liệu')
      .replace('is neither the system, an internal component nor an external entity.', 'không thuộc hệ thống, thành phần nội bộ hay thực thể bên ngoài.');
  }
  if (msg.includes('is between two external entities and is not shown in a context diagram')) {
    return msg
      .replace('Flow', 'Luồng dữ liệu')
      .replace('is between two external entities and is not shown in a context diagram.', 'nối giữa 2 thực thể bên ngoài (không được phép trong sơ đồ ngữ cảnh).');
  }
  if (msg.includes('has no data flow with the system')) {
    return msg
      .replace('External entity', 'Thực thể bên ngoài')
      .replace('has no data flow with the system.', 'chưa có luồng dữ liệu kết nối với hệ thống.');
  }

  // Use Case
  if (msg.includes('UseCase diagram requires a title')) {
    return 'Sơ đồ Use Case yêu cầu phải có tiêu đề (title).';
  }
  if (msg.includes('UseCase diagram must have at least one actor')) {
    return "Sơ đồ Use Case phải có ít nhất một tác nhân (thuộc tính 'actors').";
  }
  if (msg.includes('UseCase diagram must have at least one use case')) {
    return "Sơ đồ Use Case phải có ít nhất một ca sử dụng (thuộc tính 'useCases').";
  }
  if (msg.includes('is declared as both an actor and a use case')) {
    return msg
      .replace('Name', 'Tên')
      .replace('is declared as both an actor and a use case.', 'bị khai báo trùng lặp vừa là Actor vừa là Use Case.');
  }
  if (msg.includes("Relation must have non-empty 'from' and 'to' targets")) {
    return "Quan hệ trong sơ đồ phải có thuộc tính 'from' và 'to' không được để trống.";
  }
  if (msg.includes('cannot point to itself')) {
    return msg.replace('Relation', 'Quan hệ').replace('cannot point to itself.', 'không thể tự trỏ tới chính nó.');
  }
  if (msg.includes('must connect an actor with a use case')) {
    return msg
      .replace('Association', 'Quan hệ liên kết (association)')
      .replace("must connect an actor with a use case. Use 'include', 'extend' or 'generalization' instead.", "phải kết nối giữa một Actor với một Use Case. Sử dụng 'include', 'extend' hoặc 'generalization' nếu nối giữa 2 use case.");
  }
  if (msg.includes('must connect two use cases')) {
    return msg.replace('relation', 'quan hệ').replace('must connect two use cases.', 'phải kết nối giữa 2 Use Case.');
  }
  if (msg.includes('must connect two actors or two use cases')) {
    return msg.replace('Generalization', 'Quan hệ kế thừa').replace('must connect two actors or two use cases.', 'phải kết nối giữa 2 Actor hoặc giữa 2 Use Case.');
  }
  if (msg.includes('relations must not form a cycle')) {
    return msg.replace('relations must not form a cycle.', 'không được tạo thành vòng lặp khép kín (chu trình).');
  }

  // Sequence
  if (msg.includes('Sequence diagram requires a title')) {
    return 'Sơ đồ Sequence yêu cầu phải có tiêu đề (title).';
  }
  if (msg.includes('Sequence diagram must have at least two participants')) {
    return "Sơ đồ Sequence phải có ít nhất 2 đối tượng tham gia (thuộc tính 'participants') để gửi nhận thông điệp.";
  }
  if (msg.includes('Sequence diagram must contain at least one message')) {
    return "Sơ đồ Sequence phải có ít nhất một thông điệp (thuộc tính 'messages').";
  }
  if (msg.includes('is not in declared participants list')) {
    return msg
      .replace('Sequence message source', 'Nguồn thông điệp')
      .replace('Sequence message target', 'Đích thông điệp')
      .replace('is not in declared participants list.', 'chưa được khai báo trong danh sách participants.');
  }

  // Activity
  if (msg.includes('Activity diagram requires a title')) {
    return 'Sơ đồ Activity yêu cầu phải có tiêu đề (title).';
  }
  if (msg.includes('Activity diagram must contain at least one action step')) {
    return "Sơ đồ Activity phải có ít nhất một bước hành động (thuộc tính 'steps').";
  }

  // State
  if (msg.includes('State diagram requires a title')) {
    return 'Sơ đồ State yêu cầu phải có tiêu đề (title).';
  }
  if (msg.includes('State diagram must declare an initial state')) {
    return "Sơ đồ State phải khai báo trạng thái bắt đầu (thuộc tính 'initialState').";
  }
  if (msg.includes('State diagram must contain at least one state transition')) {
    return "Sơ đồ State phải có ít nhất một bước chuyển trạng thái (thuộc tính 'transitions').";
  }

  // Cú pháp
  if (msg.includes('Structural syntax error')) {
    return 'Lỗi cấu trúc Model JSON: ' + msg.replace('Structural syntax error:', '').trim();
  }
  if (msg.includes('Diagram model is null')) {
    return 'Nội dung mô hình sơ đồ không được để trống.';
  }

  return msg;
}
