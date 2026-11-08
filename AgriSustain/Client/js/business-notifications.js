document.addEventListener("DOMContentLoaded", async () => {
  if (!Auth.requireAuth()) return;
  const $ = (id) => document.getElementById(id);
  let page = 1;
  let total = 0;
  async function load() {
    const result = await API.get("/business-notifications?page=" + page, {
      suppressErrorToast: true,
    });
    if (!result.success) {
      $("notification-feedback").textContent = result.status
        ? result.error
        : FarmerBusinessMessages.loadFailed;
      return;
    }
    total = result.data.total;
    $("notification-feedback").textContent = total
      ? `${result.data.unread} thông báo chưa đọc`
      : FarmerBusinessMessages.noRecords;
    $("notification-list").replaceChildren();
    result.data.items.forEach((item) => {
      const card = document.createElement("article");
      card.classList.toggle("unread", !item.isRead);
      const title = document.createElement("h2");
      title.textContent = item.title;
      const content = document.createElement("p");
      content.textContent = item.content;
      const time = document.createElement("small");
      time.textContent = new Date(item.sentAt).toLocaleString("vi-VN", {
        timeZone: "Asia/Ho_Chi_Minh",
      });
      card.append(title, content, time);
      if (!item.isRead) {
        const button = document.createElement("button");
        button.textContent = "Đánh dấu đã đọc";
        button.onclick = async () => {
          const saved = await API.post(
            `/business-notifications/${item.id}/read`,
            null,
            { suppressErrorToast: true },
          );
          if (saved.success) load();
        };
        card.append(button);
      }
      $("notification-list").append(card);
    });
    $("notification-page").textContent = page;
    $("notification-prev").disabled = page <= 1;
    $("notification-next").disabled = page * 20 >= total;
  }
  $("notification-prev").onclick = () => {
    if (page > 1) {
      page--;
      load();
    }
  };
  $("notification-next").onclick = () => {
    if (page * 20 < total) {
      page++;
      load();
    }
  };
  $("notification-reload").onclick = load;
  await load();
});
